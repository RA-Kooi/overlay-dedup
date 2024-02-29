// vim: set ff=unix nobomb ts=4 sw=4
// LICENSE: GPLv3 (See LICENSE in the root of this repository)

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Security.Cryptography;

namespace overlay_dedup;

class Program
{
	static int Main(string[] args)
	{
		List<string> arguments = new(args);

		RootCommand rootCmd = new(
			"Removes files which are present in the overlay's lower "
			+ "directory from the upper directory.\n\n"
			+ "overlay-dedup  Copyright (C) 2023  Rafaël Kooi\n"
			+ "This program comes with ABSOLUTELY NO WARRANTY;\n"
			+ "This is free software, and you are welcome to redistribute it "
			+ "under certain conditions;\n"
			+ "See LICENSE or https://www.gnu.org/licenses/gpl-3.0.html");

		if(arguments.Count == 0)
			arguments.Add("-h");

		Argument<DirectoryInfo?> lowerDir = new(
			"lower dir",
			"The (read-only) lower directory.\n"
			+ "Files in this directory will take precedence over files in the "
			+ "upper directory if their modification time is newer than in "
			+ "the upper directory.");
		lowerDir.Arity = ArgumentArity.ExactlyOne;

		Argument<DirectoryInfo?> upperDir = new(
			"upper dir",
			"The (read-write) upper directory.\n"
			+ "Files in this directory will be deleted if they are older than "
			+ "their counter part in the lower directory.");
		upperDir.Arity = ArgumentArity.ExactlyOne;

		Argument<DirectoryInfo?> workDir = new(
			name: "work dir",
			description: "The work directory of the overlay.\n"
			+ "All sub directories will be deleted in this directory.\n"
			+ "Defaults to <{upper dir}/../work>.",
			getDefaultValue: () => null);
		workDir.Arity = ArgumentArity.ZeroOrOne;

		rootCmd.AddArgument(lowerDir);
		rootCmd.AddArgument(upperDir);
		rootCmd.AddArgument(workDir);

		Option<string[]> ignoreDirs = new(
			aliases: new[]{"--ignore-dirs", "-i"},
			description: "A list of directories to ignore. "
			+ "The list is absolute from the root of the mounted folder.\n"
			+ "E.g. if you want to ignore the /upper/home folder you pass "
			+ "`-i /home`\n"
			+ "To specify multiple directories pass the option multiple times.",
			getDefaultValue: () => new string[]{});
		ignoreDirs.Arity = ArgumentArity.OneOrMore;

		Option<string[]> keepFiles = new(
			aliases: new[]{"--keep-files", "-k"},
			description: "A list of files to keep. Files which would be "
			+ "otherwise overwritten will now get a sibling with the .new "
			+ "extension.\nTo change the extension see --backup-ext.\n"
			+ "The file list is absolute from the root of the mounted folder.\n"
			+ "E.g. if you want ignore /upper/etc/shadow you pass "
			+ "`-k /etc/shadow`\n"
			+ "To specify multiple files pass the option multiple times.\n"
			+ "!IMPORTANT!: If the backup file already exists it will be "
			+ "overwritten.",
			getDefaultValue: () => new string[]{});
		keepFiles.Arity = ArgumentArity.OneOrMore;

		Option<string> backupExt = new(
			aliases: new[]{"--backup-ext", "-b"},
			description: "Alternate backup extension without leading dot.",
			getDefaultValue: () => new("new"));
		backupExt.Arity = ArgumentArity.ExactlyOne;

		Option<bool> dryRun = new(
			aliases: new[]{"--dry-run", "-n"},
			description: "Don't delete files, but output what it would delete.",
			getDefaultValue: () => false);
		dryRun.Arity = ArgumentArity.Zero;

		Option<bool> verbose = new(
			aliases: new[]{"--verbose", "-v"},
			description: "Show verbose messages (paths, etc)",
			getDefaultValue: () => false);
		verbose.Arity = ArgumentArity.Zero;

		rootCmd.AddOption(ignoreDirs);
		rootCmd.AddOption(keepFiles);
		rootCmd.AddOption(backupExt);
		rootCmd.AddOption(dryRun);
		rootCmd.AddOption(verbose);

		CommandLineBuilder builder = new(rootCmd);
		Parser parser = builder
			.UseDefaults()
			.UseHelp(
				ctx =>
				{
					// Strip the default arguments from the output.
					ctx.HelpBuilder.CustomizeSymbol(
						ignoreDirs,
						secondColumnText: ignoreDirs.Description);

					ctx.HelpBuilder.CustomizeSymbol(
						keepFiles,
						secondColumnText: keepFiles.Description);
				})
			.UseExceptionHandler(
				(ex, ctx) =>
				{
					Console.ForegroundColor = ConsoleColor.Red;

					if(ex is DirectoryNotFoundException)
					{
						Console.Error.WriteLine($"ERROR: {ex.Message}");
						return;
					}

					Console.Error.WriteLine(
						$"Unhandled exception: {ex.GetType().ToString()}: "
						+ $"{ex.Message}");
					Console.Error.WriteLine(ex.StackTrace?.ToString());
				}, 1)
			.Build();

		rootCmd.SetHandler(
			(DirectoryInfo? lowerDir,
			 DirectoryInfo? upperDir,
			 DirectoryInfo? workDir,
			 string[] ignoreDirs,
			 string[] keepFiles,
			 string backupExt,
			 bool dryRun,
			 bool verbose) =>
			{
				if(!lowerDir!.Exists)
				{
					throw new DirectoryNotFoundException(
						"Lower directory does not exist");
				}

				if(!upperDir!.Exists)
				{
					throw new DirectoryNotFoundException(
						"Upper directory does not exist");
				}

				if(workDir == null)
				{
					workDir = new(upperDir.FullName + "/../work");
				}

				if(!workDir.Exists)
				{
					throw new DirectoryNotFoundException(
						"Work directory does not exist");
				}

				ignoreDirs = ignoreDirs
					.Select(dir => dir.TrimEnd('/'))
					.ToArray();

				Deduplicate(
					lowerDir!,
					upperDir!,
					ignoreDirs,
					keepFiles,
					backupExt,
					dryRun,
					verbose);

				ClearWorkDir(workDir);
			},
			lowerDir,
			upperDir,
			workDir,
			ignoreDirs,
			keepFiles,
			backupExt,
			dryRun,
			verbose);

		return parser.Invoke(args);
	}

	static void Deduplicate(
		DirectoryInfo lowerDir,
		DirectoryInfo upperDir,
		string[] ignoreDirs,
		string[] keepFiles,
		string backupExt,
		bool dryRun,
		bool verbose)
	{
		RemoveEmptyDirs(upperDir, ignoreDirs, dryRun, verbose);

		bool Recurse(string currentPath)
		{
			if(ignoreDirs.Contains(currentPath))
			{
				if(verbose)
					Console.WriteLine($"Skipping ignored dir: {currentPath}\n");

				return false;
			}

			DirectoryInfo currentUpper = new(upperDir.FullName + currentPath);
			DirectoryInfo currentLower = new(lowerDir.FullName + currentPath);

			HashSet<DirectoryInfo> upperDirs = currentUpper
				.EnumerateDirectories()
				.ToHashSet();

			HashSet<DirectoryInfo> lowerDirs = currentLower
				.EnumerateDirectories()
				.ToHashSet();

			IEnumerable<DirectoryInfo> bothDirs = upperDirs
				.Intersect(lowerDirs, new FSNameComparer<DirectoryInfo>());

			int dirCount = 0, removedDirCount = 0;
			foreach(DirectoryInfo child in bothDirs)
			{
				dirCount++;

				if(Recurse($"{currentPath}/{child.Name}"))
					removedDirCount++;
			}

			HashSet<FileInfo> upperFiles = currentUpper
				.EnumerateFiles()
				.ToHashSet();

			HashSet<FileInfo> lowerFiles = currentLower
				.EnumerateFiles()
				.ToHashSet();

			IEnumerable<FileInfo> bothFiles = upperFiles
				.Intersect(lowerFiles, new FSNameComparer<FileInfo>());

			if(verbose)
			{
				string verbosePath = currentPath == "" ? "/" : currentPath;
				Console.WriteLine($"In dir: {verbosePath}");
			}

			int fileCount = 0, removedFileCount = 0;
			foreach(FileInfo child in bothFiles)
			{
				fileCount++;

				FileInfo lowerFile = new FileInfo(
					$"{currentLower.FullName}/{child.Name}");

				FileInfo upperFile = new FileInfo(
					$"{currentUpper.FullName}/{child.Name}");

				if(verbose)
				{
					Console.WriteLine($"Child: {currentPath}/{child.Name}");

					Console.WriteLine($"Lower exists: {upperFile.Exists}");
					Console.WriteLine($"Upper exists: {upperFile.Exists}");
				}

				if(keepFiles.Contains($"{currentPath}/{child.Name}"))
				{
					string newPath = $"{upperFile.FullName}.{backupExt}";

					if(dryRun)
					{
						Console.WriteLine(
							$"Would copy {lowerFile.FullName} to {newPath}\n");
					}
					else
					{
						if(verbose)
						{
							Console.WriteLine(
								$"Copying {lowerFile.FullName} to {newPath}\n");
						}

						lowerFile.CopyTo(newPath, true);
					}

					continue;
				}

				if(verbose)
				{
					long ftime = lowerFile.LastWriteTimeUtc.ToFileTimeUtc();
					Console.WriteLine($"Modification time (lower): {ftime}");

					ftime = upperFile.LastWriteTimeUtc.ToFileTimeUtc();
					Console.WriteLine($"Modification time (upper): {ftime}");
				}

				if(lowerFile.LastWriteTimeUtc >= upperFile.LastWriteTimeUtc)
				{
					if(dryRun)
						Console.WriteLine($"Would remove: {upperFile.FullName}\n");
					else
					{
						if(verbose)
							Console.WriteLine($"Deleting: {upperFile.FullName}\n");

						upperFile.Delete();
					}

					removedFileCount++;
					continue;
				}

				if(lowerFile.LinkTarget == upperFile.LinkTarget
				   && lowerFile.LinkTarget != null)
				{
					if(verbose)
						Console.WriteLine("Skipping identical symlink\n");

					continue;
				}

				if(lowerFile.Length == upperFile.Length)
				{
					if(lowerFile.Length == 0)
					{
						Statx stat;

						libC.statx(
							-1,
							lowerFile.FullName,
							0x800,              // AT_NO_AUTOMOUNT
							1,                  // STATX_TYPE
							out stat);

						if((stat.Mode & 61440) == 49152)
						{
							if(verbose)
								Console.WriteLine("Skipping socket\n");

							continue;
						}
					}

					if(verbose)
					{
						Console.WriteLine(
							"Lower and upper are the same size, "
							+ "doing hash check");
					}

					using (SHA256 lowerSha = SHA256.Create())
					using (SHA256 upperSha = SHA256.Create())
					using (FileStream lowerStream = lowerFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
					using (FileStream upperStream = upperFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						lowerStream.Position = 0;
						upperStream.Position = 0;
						byte[] lowerHash = lowerSha.ComputeHash(lowerStream);
						byte[] upperHash = upperSha.ComputeHash(upperStream);

						if(lowerHash.SequenceEqual(upperHash))
						{
							if(verbose)
								Console.WriteLine("Hashes equal, deleting...");

							if(dryRun)
							{
								Console.WriteLine(
									$"Would remove: {upperFile.FullName}\n");
							}
							else
							{
								if(verbose)
								{
									Console.WriteLine(
										$"Deleting: {upperFile.FullName}\n");
								}

								upperFile.Delete();
							}

							removedFileCount++;
						}
						else if(verbose)
							Console.WriteLine("Hashes are unique\n");
					}
				}
			}

			if(upperFiles.Count == fileCount
			   && removedFileCount == fileCount
			   && upperDirs.Count == dirCount
			   && removedDirCount == dirCount
			   && currentPath != "")
			{
				if(dryRun)
				{
					Console.WriteLine($"Would delete dir: {currentUpper.FullName}\n");
					return true;
				}

				try
				{
					if(verbose)
						Console.WriteLine($"Deleting dir: {currentUpper.FullName}\n");

					currentUpper.Delete();
					return true;
				}
				catch(IOException)
				{
					Debug.Assert(
						false,
						$"Failed to delete <{currentUpper.FullName}> because it's "
						+ "not empty\n");

					return false;
				}
			}

			if(verbose)
				Console.WriteLine("");

			return false;
		}

		if(verbose)
			Console.WriteLine("Start deduplication:");

		Recurse("");
	}

	static void RemoveEmptyDirs(
		DirectoryInfo upperDir,
		string[] ignoreDirs,
		bool dryRun,
		bool verbose)
	{
		bool Recurse(string currentPath)
		{
			if(ignoreDirs.Contains(currentPath))
			{
				if(verbose)
					Console.WriteLine($"\nSkipping ignored dir: {currentPath}");

				return false;
			}

			DirectoryInfo currentDir = new(upperDir.FullName + currentPath);

			int dirCount = 0, removedDirCount = 0;
			foreach(DirectoryInfo child in currentDir.EnumerateDirectories())
			{
				++dirCount;

				if(Recurse($"{currentPath.TrimEnd('/')}/{child.Name}"))
					++removedDirCount;
			}

			int fileCount = currentDir.EnumerateFiles().Count();
			if(fileCount > 0)
				return false;

			if(dirCount == removedDirCount)
			{
				if(verbose)
					Console.WriteLine("");

				if(dryRun)
				{
					Console.WriteLine(
						$"Would delete dir: {upperDir.Name}{currentPath}");

					return true;
				}

				try
				{
					if(verbose)
					{
						Console.WriteLine(
							$"Deleting dir: {upperDir.Name}{currentPath}");
					}

					currentDir.Delete();
					return true;
				}
				catch(IOException)
				{
					Debug.Assert(
						false,
						$"Failed to delete <{currentDir.FullName}>"
						+ "because it's not empty");

					return false;
				}
			}

			return false;
		}


		if(verbose)
			Console.WriteLine("Remove empty directories from upper dir:");

		Recurse("/");

		if(verbose)
			Console.WriteLine("");
	}

	static void ClearWorkDir(DirectoryInfo workDir)
	{
		foreach(FileInfo child in workDir.EnumerateFiles())
		{
			child.Delete();
		}

		foreach(DirectoryInfo child in workDir.EnumerateDirectories())
		{
			child.Delete(true);
		}
	}
}
