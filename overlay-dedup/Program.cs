// vim: set ff=unix nobomb ts=4 sw=4
// LICENSE: GPLv3 (See LICENSE in the root of this repository)

using System.Collections.Generic;
using System.CommandLine;

namespace overlay_dedup;

class Program
{
	static int Main(string[] args)
	{
		List<string> arguments = new(args);

		RootCommand rootCmd = new(
			"overlay-dedup  Copyright (C) 2023  Rafaël Kooi\n"
			+ "This program comes with ABSOLUTELY NO WARRANTY;\n"
			+ "This is free software, and you are welcome to redistribute it "
			+ "under certain conditions;\n"
			+ "See LICENSE or https://www.gnu.org/licenses/gpl-3.0.html");

		if(arguments.Count == 0)
			arguments.Add("-h");

		return rootCmd.Invoke(arguments.ToArray());
	}
}
