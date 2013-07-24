
using Challonge.Data;

namespace Challonge
{
	public sealed class Station
	{
		public int Id { get; set; }
		public ChallongeClient.Match Match { get; set; }
	}
}
