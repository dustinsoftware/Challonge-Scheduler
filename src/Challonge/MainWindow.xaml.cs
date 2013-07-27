using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Challonge.Data;
using Challonge.Properties;
using log4net;
using log4net.Config;
using Timer = System.Timers.Timer;

namespace Challonge
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			XmlConfigurator.Configure();

			s_log.Info("Starting up.");

			m_client = new ChallongeClient();

			Task.Factory.StartNew(() => OnTimerElapsed(null, null));

			m_timer = new Timer(5000);
			m_timer.Elapsed += OnTimerElapsed;
			m_timer.Start();

			s_log.InfoFormat("Creating {0} stations.", Settings.Default.Stations);
			m_stations = Enumerable.Range(0, Settings.Default.Stations)
				.Select((x, i) => new Station { Id = i + 1 })
				.ToList();
		}

		protected override void OnClosed(EventArgs e)
		{
			m_timer.Dispose();
			base.OnClosed(e);
		}

		private void OnTimerElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			if (m_busy)
				return;

			m_busy = true;
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => StatusEllipse.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 0))));

			IEnumerable<ChallongeClient.Match> totalMatches = m_client.GetMatches("all");
			if (totalMatches == null)
			{
				s_log.Error("Unable to get all matches.");
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => StatusEllipse.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0))));
				m_busy = false;
				return;
			}

			var totalMatchesCollection = new ReadOnlyCollection<ChallongeClient.Match>(totalMatches.ToList());
			int winnersFinals = totalMatchesCollection.Count == 0 ? 0 : totalMatchesCollection.Max(x => x.round);
			int losersFinals = totalMatchesCollection.Count == 0 ? 0 : totalMatchesCollection.Min(x => x.round);

			IEnumerable<ChallongeClient.Match> openMatches = totalMatchesCollection.Where(x => x.state == "open");
			ReadOnlyCollection<ChallongeClient.Match> openMatchesCollection = openMatches == null ? null :
				new ReadOnlyCollection<ChallongeClient.Match>(openMatches
					.OrderBy(x => x.started_at).ToList());

			if (openMatchesCollection == null || openMatchesCollection.Any(x => m_client.GetParticipant(x.player1_id) == null || m_client.GetParticipant(x.player2_id) == null))
			{
				s_log.ErrorFormat("Unable to retrieve matches or participants.  Matches: {0}", openMatchesCollection);
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => StatusEllipse.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0))));
				m_busy = false;
				return;
			}

			foreach (var completedMatch in m_stations.Where(x => x.Match != null && !openMatchesCollection.Select(openMatch => openMatch.id).Contains(x.Match.id)))
				completedMatch.Match = null;

			Queue<ChallongeClient.Match> waitingMatches = new Queue<ChallongeClient.Match>(openMatchesCollection
				.Where(match => !IsFinalsRound(match.round, winnersFinals, losersFinals) && !m_stations
					.Where(station => station.Match != null)
					.Select(station => station.Match.id).Contains(match.id)));

			foreach (var station in m_stations.Where(x => x.Match == null).TakeWhile(x => waitingMatches.Count != 0))
				station.Match = waitingMatches.Dequeue();

			if (openMatchesCollection.All(x => IsFinalsRound(x.round, winnersFinals, losersFinals)))
			{
				// everyone has finished, start playing the finals on the first TV
				m_stations.First().Match = openMatchesCollection.FirstOrDefault();
				foreach (var openMatch in openMatchesCollection.Except(new[] { m_stations.First().Match }))
					waitingMatches.Enqueue(openMatch);
			}
			else
			{
				foreach (var finalsMatch in openMatchesCollection.Where(x => IsFinalsRound(x.round, winnersFinals, losersFinals)))
					waitingMatches.Enqueue(finalsMatch);
			}

			List<string> matchStrings = m_stations.Select(station =>
				{
					if (station.Match == null)
						return string.Format("Station {0} - Not scheduled.", station.Id);

					return string.Format("Station {0} - {1}: {2} vs {3}", station.Id,
						GetRoundString(station.Match.round, winnersFinals, losersFinals),
						m_client.GetParticipant(station.Match.player1_id).name,
						m_client.GetParticipant(station.Match.player2_id).name
						);
				}).Concat(new[] { "", "Next up:" })
				.Concat(waitingMatches
					.Select(x => string.Format("{0}: {1} vs {2}",
						GetRoundString(x.round, winnersFinals, losersFinals),
						m_client.GetParticipant(x.player1_id).name,
						m_client.GetParticipant(x.player2_id).name)))
				.ToList();

			Dispatcher.Invoke(DispatcherPriority.Normal, new Action<IEnumerable<string>>(items =>
			{
				Matches.Text = string.Join(Environment.NewLine, items);
				StatusEllipse.Fill = new SolidColorBrush(Color.FromRgb(0, 255, 0));
			}), matchStrings);

			m_busy = false;

			s_log.Info("Update finished normally.");
		}

		private static string GetRoundString(int round, int winnersFinals, int losersFinals)
		{
			if (round == winnersFinals - 1)
				return "Winners Finals";
			if (round == winnersFinals - 2)
				return "Winners Semis";
			if (round == losersFinals)
				return "Losers Finals";
			if (round == losersFinals + 1 || round == losersFinals + 2)
				return "Losers Semis";
			if (round >= winnersFinals)
				return "Grand Finals";

			return string.Format("Round {0}", round < 0 ? "L" + Math.Abs(round) : "W" + round);
		}

		private void StatusEllipse_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			bool fullScreen = WindowStyle == WindowStyle.None;

			if (fullScreen)
			{
				WindowStyle = WindowStyle.SingleBorderWindow;
				Topmost = false;
			}
			else
			{
				WindowStyle = WindowStyle.None;
				Topmost = true;
			}
		}

		private static bool IsFinalsRound(int round, int winnersFinal, int losersFinal)
		{
			return round >= winnersFinal - 1 || round <= losersFinal;
		}

		static readonly ILog s_log = LogManager.GetLogger(typeof(MainWindow));

		readonly Timer m_timer;
		readonly ChallongeClient m_client;
		readonly List<Station> m_stations;

		bool m_busy;
	}
}
