using System.Text.Json.Serialization;

namespace Shared
{
	public class CatResponse
	{
		[JsonPropertyName("fact")]
		public string Fact { get; set; }
		[JsonPropertyName("length")]
		public int Length { get; set; }
	}
}
