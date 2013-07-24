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

			m_client = new ChallongeClient();

			Task.Factory.StartNew(() => OnTimerElapsed(null, null));

			m_timer = new Timer(5000);
			m_timer.Elapsed += OnTimerElapsed;
			m_timer.Start();

			m_stations = Enumerable.Range(0, Settings.Default.Stations)
				.Select(x => new Station())
				.ToList();
		}

		protected override void OnClosed(EventArgs e)
		{
			m_timer.Dispose();
			base.OnClosed(e);
		}

		private void OnTimerElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => StatusEllipse.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 0))));

			IEnumerable<ChallongeClient.Match> matches = m_client.GetMatches();
			ReadOnlyCollection<ChallongeClient.Match> openMatches = matches == null ? null : new ReadOnlyCollection<ChallongeClient.Match>(matches
				.OrderBy(x => x.started_at).ToList());

			if (openMatches == null || openMatches.Any(x => m_client.GetParticipant(x.player1_id) == null || m_client.GetParticipant(x.player2_id) == null))
			{
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => StatusEllipse.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0))));
				return;
			}

			foreach (var completedMatch in m_stations.Where(x => x.Match != null && !openMatches.Select(openMatch => openMatch.id).Contains(x.Match.id)))
				completedMatch.Match = null;

			Queue<ChallongeClient.Match> waitingMatches = new Queue<ChallongeClient.Match>(openMatches
				.Where(match => !m_stations
					.Where(station => station.Match != null)
					.Select(station => station.Match.id).Contains(match.id)));

			foreach (var station in m_stations.Where(x => x.Match == null).TakeWhile(x => waitingMatches.Count != 0))
				station.Match = waitingMatches.Dequeue();

			List<string> matchStrings = m_stations.Select((station, i) =>
				{
					if (station.Match == null)
						return string.Format("Station {0}: Not scheduled.", i + 1);

					return string.Format("Station {0}: {1} vs {2}", i + 1,
						m_client.GetParticipant(station.Match.player1_id).name,
						m_client.GetParticipant(station.Match.player2_id).name
						);
				}).Concat(new[] { "", "Next up:" })
				.Concat(waitingMatches
					.Select(x => string.Format("{0} vs {1}",
						m_client.GetParticipant(x.player1_id).name,
						m_client.GetParticipant(x.player2_id).name)))
				.ToList();

			Dispatcher.Invoke(DispatcherPriority.Normal, new Action<IEnumerable<string>>(items =>
			{
				Matches.Text = string.Join(Environment.NewLine, items);
				StatusEllipse.Fill = new SolidColorBrush(Color.FromRgb(0, 255, 0));
			}), matchStrings);
		}

		readonly Timer m_timer;
		readonly ChallongeClient m_client;
		readonly List<Station> m_stations;
	}
}
