using Spectre.Console;
using System;

namespace Shared
{
	public static class UtilsConsole
	{
		public static bool PrintContinueToAskPrompt()
		{
			return PrintPrompt("Continue to ask?");
		}

		public static void PrintHello()
		{
			AnsiConsole.Write(
								new FigletText("Spiritual cat")
								.LeftJustified()
								.Color(Color.Red));


			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[underline yellow]Hi,[/] [green]my cat will try to give you a fact from cat life[/].");

			AnsiConsole.WriteLine();
		}

		public static void PrintBye()
		{
			AnsiConsole.MarkupLine(string.Empty);

			AnsiConsole.MarkupLine("[yellow]Thanks[/] for using this sample and [orange3]bye[/]!");
		}

		private static bool PrintPrompt(string title)
		{
			var confirmation = AnsiConsole.Prompt(
													new TextPrompt<bool>(title)
														.AddChoice(true)
														.AddChoice(false)
														.DefaultValue(true)
														.WithConverter(choice => choice ? "y" : "n"));

			return confirmation;
		}

		public static void PrintCancelAsking(bool dueToTimeout = false)
		{
			AnsiConsole.MarkupLine($"Asking was [dodgerblue1]canceled[/]{(dueToTimeout ? " probably due to timeout" : string.Empty)}.");
		}

		public static void PrintNoCorrectAnswer(Exception error)
		{
			AnsiConsole.MarkupLine("The question to my cat led to [underline red]error[/].");
			AnsiConsole.WriteException(error, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes |
												ExceptionFormats.ShortenMethods | ExceptionFormats.ShowLinks | ExceptionFormats.NoStackTrace);
			Console.WriteLine("----------------------------------------");
			AnsiConsole.MarkupLine("The cat has [underline red]no correct[/] answer in this session.");
		}
	}
}
