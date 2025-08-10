namespace Shared
{
	internal static class CatFactUriMutator
	{
		internal static string GetCatFactUri()
		{
			var rw = Utils.Randomizer.Next();
			//Imitate that sometimes cat has no answer
			return rw < 3 ? "nofact" : "fact";
		}
	}
}
