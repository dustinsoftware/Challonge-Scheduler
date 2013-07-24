using System;
using System.Collections.Generic;
using System.Linq;
using Challonge.Data.Properties;
using RestSharp;

namespace Challonge.Data
{
	public sealed class ChallongeClient
	{
		public ChallongeClient()
		{
			m_client = new RestClient
			{
				BaseUrl = Settings.Default.BaseUri + Settings.Default.TournamentId + "/",
			};
			m_participants = new Dictionary<int, Participant>();
		}

		public IEnumerable<Match> GetMatches(string state)
		{
			var result = m_client.Get<List<MatchResult>>(new RestRequest(string.Format("matches.json?state={0}", state) + s_apiKeyString));
			return result.ResponseStatus != ResponseStatus.Completed ?
				null :
				result.Data.Select(x => x.match);
		}

		public Participant GetParticipant(int playerId)
		{
			lock (m_lock)
			{
				Participant participant;
				if (!m_participants.TryGetValue(playerId, out participant))
				{
					var response = m_client.Get<ParticipantResult>(new RestRequest(string.Format("participants/{0}.json?state=open{1}", playerId, s_apiKeyString))).Data;
					if (response != null)
					{
						participant = response.participant;
						m_participants.Add(playerId, participant);
					}
				}
				return participant;
			}
		}

		public class Match
		{
			public int player1_id { get; set; }
			public int player2_id { get; set; }
			public int round { get; set; }
			public int id { get; set; }
			public DateTime started_at { get; set; }
		}

		public class MatchResult
		{
			public Match match { get; set; }
		}

		public class Participant
		{
			public bool active { get; set; }
			public bool checked_in { get; set; }
			public string created_at { get; set; }
			public object final_rank { get; set; }
			public object group_id { get; set; }
			public object icon { get; set; }
			public int id { get; set; }
			public object invitation_id { get; set; }
			public object invite_email { get; set; }
			public object misc { get; set; }
			public string name { get; set; }
			public bool on_waiting_list { get; set; }
			public int seed { get; set; }
			public int tournament_id { get; set; }
			public string updated_at { get; set; }
		}

		public class ParticipantResult
		{
			public Participant participant { get; set; }
		}

		static readonly string s_apiKeyString = "&api_key=" + Settings.Default.ApiKey;

		readonly RestClient m_client;
		readonly object m_lock = new object();
		readonly Dictionary<int, Participant> m_participants;
	}
}
