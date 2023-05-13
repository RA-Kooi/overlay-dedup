// vim: set ff=unix nobomb ts=4 sw=4
// LICENSE: GPLv3 (See LICENSE in the root of this repository)

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.IO;

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

		rootCmd.AddArgument(lowerDir);
		rootCmd.AddArgument(upperDir);

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
			.Build();

		rootCmd.SetHandler(
			(DirectoryInfo? lowerDir,
			 DirectoryInfo? upperDir,
			 string[] ignoreDirs,
			 string[] keepFiles,
			 string backupExt,
			 bool dryRun,
			 bool verbose) =>
			{
				Deduplicate(
					lowerDir!,
					upperDir!,
					ignoreDirs,
					keepFiles,
					backupExt,
					dryRun,
					verbose);
			},
			lowerDir,
			upperDir,
			ignoreDirs,
			keepFiles,
			backupExt,
			dryRun,
			verbose);

		return parser.Invoke(args);
	}
}
