using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCAA2022
{
    internal class Program
    {
        public static Random R;

        public static bool VERBOSE = false;
        public static char GEN = 'W';
        public static string YEAR = "2022";
        public static int SIMS = 5000;
        public static HashSet<string> wleft = new HashSet<string> { "Greensboro", "Wichita" };
        public static HashSet<string> mleft = new HashSet<string> { "West", "East" };

        static void Main(string[] args)
        {
            R = new Random();
            Dictionary<string, Team> teams = new Dictionary<string, Team>();
            Dictionary<string, Team> seeds = new Dictionary<string, Team>();

            using (var reader = new StreamReader(@"Data\"+GEN + "NCAATourneySeeds.csv"))
            {
                reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    string[] values = line.Split(',');
                    if (values[0].Equals(Program.YEAR))
                    {
                        teams.Add(values[2], new Team(values[1], 6));
                        seeds.Add(values[1], teams[values[2]]);
                    }

                }
            }
            Dictionary<string, string> names = new Dictionary<string, string>();
            using (var reader = new StreamReader(@"Data\" + GEN + "TeamSpellings.csv"))
            {
                reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    string[] values = line.Split(',');
                    names.Add(values[0], values[1]);
                }
            }

            using (var reader = new StreamReader(@"Data\" + "fivethirtyeight_ncaa_forecasts.csv"))
            {
                reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    string[] values = line.Split(',');
                    if ((values[0].Equals("womens") && GEN == 'W') || (values[0].Equals("mens") && GEN == 'M'))
                    {
                        Team cur = teams[names[values[13].ToLower()]];
                        for (int i = 0; i < cur.rounds.Length; i++)
                        {
                            cur.rounds[i] = double.Parse(values[i + 4]);
                        }
                        cur.name = values[13];
                        cur.Wseed = int.Parse(values[16].Substring(0, Math.Min(2, values[16].Length)));
                        cur.region = values[15];
                    }
                }
            }
            List<Team> teamList = teams.Values.ToList<Team>();
            for (int a = 0; a < teamList.Count; a++)
            {
                Team team1 = teamList[a];
                for (int b = a + 1; b < teamList.Count; b++)
                {
                    Team team2 = teamList[b];
                    int nextround = team1.meet(team2, GEN) + 1;
                    while (nextround < team1.otherWin.Length)
                    {
                        team1.otherWin[nextround] += team2.rounds[nextround];
                        team2.otherWin[nextround] += team1.rounds[nextround];
                        nextround++;
                    }
                }
            }

            if (VERBOSE)
            {
                foreach (Team t in teams.Values)
                {
                    Console.WriteLine(t);
                }
            }
            Dictionary<string, int> overall = new Dictionary<string, int>();
            DateTime startTime, endTime;
            startTime = DateTime.Now;

            for (int b = 0; b < SIMS; b++)
            {
                if (b % 1000 == 0)
                {
                    endTime = DateTime.Now;
                    Console.WriteLine(string.Format("{0} at {1:0.0}", b, ((TimeSpan)(endTime - startTime)).TotalMilliseconds / 1000.0));
                }
                
                Team[][] winners = WgenerateWinners(seeds, true);
                SortedList<double, string> trial = new SortedList<double, string>();

                double loff = 50;
                double lfactor = .9;

                while (loff > 0)
                {
                    if (loff < 2)
                    {
                        loff = 0;
                    }
                    double offset = loff / 100.0;
                    double ptotal = 0;
                    double ntotal = 0;
                    foreach (Team[] result in winners)
                    {
                        double pscore = linearScoring(result[0], result[1], offset);
                        double nscore = linearScoring(result[0], result[1], -offset);

                        ptotal += Math.Log(pscore);
                        ntotal += Math.Log(nscore);
                    }
                    ptotal /= -winners.Length;
                    ntotal /= -winners.Length;

                    trial.Add(ptotal, "L" + offset);
                    if (loff != 0)
                    {
                        trial.Add(ntotal, "L" + (-offset));
                    }
                    loff *= lfactor;
                }

                double poff = .5;
                double pfactor = .9;
                bool doPL = true;
                while (poff > .02)
                {
                    double ptotal = 0;
                    double ntotal = 0;
                    foreach (Team[] result in winners)
                    {
                        double pscore = percentScoring(result[0], result[1], poff);
                        double nscore = percentScoring(result[0], result[1], -poff);

                        ptotal += Math.Log(pscore);
                        ntotal += Math.Log(nscore);
                    }
                    ptotal /= -winners.Length;
                    ntotal /= -winners.Length;

                    trial.Add(ptotal, "P" + poff);
                    trial.Add(ntotal, "P" + -poff);

                    poff *= pfactor;

                    loff = 40;
                    lfactor = .8;
                    if (doPL)
                    {
                        while (loff > 1)
                        {
                            double offset = loff / 100.0;
                            double pptotal = 0;
                            double pntotal = 0;
                            double nptotal = 0;
                            double nntotal = 0;
                            foreach (Team[] result in winners)
                            {
                                double ppscore = plScoring(result[0], result[1], poff, offset);
                                double pnscore = plScoring(result[0], result[1], poff, -offset);
                                double npscore = plScoring(result[0], result[1], -poff, offset);
                                double nnscore = plScoring(result[0], result[1], -poff, -offset);

                                pptotal += Math.Log(ppscore);
                                pntotal += Math.Log(pnscore);
                                nptotal += Math.Log(npscore);
                                nntotal += Math.Log(nnscore);
                            }
                            pptotal /= -winners.Length;
                            pntotal /= -winners.Length;
                            nptotal /= -winners.Length;
                            nntotal /= -winners.Length;

                            trial.Add(pptotal, "PL" + poff + "," + offset);
                            trial.Add(pntotal, "PL" + poff + "," + -offset);
                            trial.Add(nptotal, "PL" + -poff + "," + offset);
                            trial.Add(nntotal, "PL" + -poff + "," + -offset);

                            loff *= lfactor;
                        }
                    }
                }
                int val = 10;
                for (int c = 0; c < 3; c++)
                {
                    string cur = trial.Values[c];
                    if (!overall.Keys.Contains(cur))
                    {
                        overall.Add(cur, 0);
                    }
                    overall[cur] += val;
                    val /= 2;
                }

            }
            var sortedOver = (from kvp in overall orderby kvp.Value descending select kvp).ToArray();
            Console.WriteLine("Number of entries: " + sortedOver.Length);
            for (int a = 0; a < Math.Min(25, sortedOver.Length); a++)
            {
                Console.WriteLine("{0} {1}", sortedOver[a].Key, sortedOver[a].Value);
            }
            Console.WriteLine(sortedOver[0].Key);
            WwriteToFile(teams, 0, sortedOver[0].Key);

            WprintBracket(seeds, sortedOver[0].Key);
            Console.Read();
        }

        public static double Wscoring(Team team1, Team team2)
        {
            int round = team1.meet(team2, GEN);
            double t1 = team1.rounds[round];
            double t2 = team2.rounds[round];
            if (round != 0)
            {
                t1 /= team1.rounds[round - 1];
                t2 /= team2.rounds[round - 1];

                var x = team1.rounds[round] / (team1.rounds[round] + team1.otherWin[round]);
                x *= (team2.rounds[round - 1] - team2.rounds[round]);
                var z = team2.rounds[round] / (team2.rounds[round] + team2.otherWin[round]);
                z *= (team1.rounds[round - 1] - team1.rounds[round]);
                return x / (x + z);
            }
            return t1 / (t1 + t2);
        }

        private static double Wscoring(Team team1, Team team2, string scoring)
        {
            double score;
            if (scoring.StartsWith("L"))
            {
                score = linearScoring(team1, team2, double.Parse(scoring.Substring(1)));
            }
            else if (scoring.StartsWith("PL"))
            {
                string[] doubs = scoring.Substring(2).Split(',');
                score = plScoring(team1, team2, double.Parse(doubs[0]), double.Parse(doubs[1]));
            }
            else
            {
                score = percentScoring(team1, team2, double.Parse(scoring.Substring(1)));
            }
            return score;
        }

        private static double plScoring(Team team1, Team team2, double factor, double offset)
        {
            double score = Wscoring(team1, team2);

            double diff = Math.Abs((2 * score) - 1);
            double totaloffset = (diff * factor) + offset;
            score += score < .5 ? -totaloffset : totaloffset;
            score = Math.Max(score, 0.000001);
            score = Math.Min(score, .9999999);
            return score;
        }

        private static double linearScoring(Team team1, Team team2, double offset)
        {
            double score = Wscoring(team1, team2);
            score += score < .5 ? -offset : offset;
            score = Math.Max(score, 0.000001);
            score = Math.Min(score, .9999999);
            return score;
        }

        private static double percentScoring(Team team1, Team team2, double factor)
        {
            double score = Wscoring(team1, team2);
            double diff = Math.Abs((2 * score) - 1);
            double offset = diff * factor;
            score += score < .5 ? -offset : offset;
            score = Math.Max(score, 0.000001);
            score = Math.Min(score, .9999999);
            return score;
        }

        public static void WwriteToFile(Dictionary<string, Team> teams, int eloindex, string scoring)
        {
            string submissionfile = @"Data\" + GEN + "SampleSubmissionStage2.csv";
            string outputfile = GEN + "SubmissionStage2.csv";

            using (var reader = new StreamReader(submissionfile))
            {
                using (StreamWriter writer = new StreamWriter(outputfile))
                {
                    writer.WriteLine(reader.ReadLine());
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        string[] values = line.Split(',');
                        string[] values2 = values[0].Split('_');
                        double score = Wscoring(teams[values2[1]], teams[values2[2]], scoring);

                        if (score < .0001)
                        {
                            score = .0001;
                        }
                        if (score > .9999)
                        {
                            score = .9999;
                        }
                        if (VERBOSE && false)
                        {
                            Console.WriteLine(teams[values2[1]] + " " + teams[values2[2]] + " " + score + " " + Math.Log(score));
                        }
                        writer.WriteLine(values[0] + "," + score);

                    }
                }
            }
        }

        private static void WprintBracket(Dictionary<string, Team> seeds, string scoring)
        {
            List<Team> ff = new List<Team>();
            foreach (string reg in new string[] { "W", "X", "Y", "Z" })
            {
                Console.WriteLine(reg + ": First round");
                Team s16;
                if (!seeds.ContainsKey(reg + "16"))
                {
                    s16 = WprintMatchup(seeds[reg + "16a"], seeds[reg + "16b"], scoring);
                }
                else
                {
                    s16 = seeds[reg + "16"];
                }
                Team s11;
                if (!seeds.ContainsKey(reg + "11"))
                {
                    s11 = WprintMatchup(seeds[reg + "11a"], seeds[reg + "11b"], scoring);
                }
                else
                {
                    s11 = seeds[reg + "11"];
                }
                Team s12;
                if (!seeds.ContainsKey(reg + "12"))
                {
                    s12 = WprintMatchup(seeds[reg + "12a"], seeds[reg + "12b"], scoring);
                }
                else
                {
                    s12 = seeds[reg + "12"];
                }

                Team winner1 = WprintMatchup(seeds[reg + "01"], s16, scoring);
                Team winner2 = WprintMatchup(seeds[reg + "08"], (seeds[reg + "09"]), scoring);
                Team winner3 = WprintMatchup(seeds[reg + "05"], s12, scoring);
                Team winner4 = WprintMatchup(seeds[reg + "04"], (seeds[reg + "13"]), scoring);

                Team winner5 = WprintMatchup(seeds[reg + "06"], s11, scoring);
                Team winner6 = WprintMatchup(seeds[reg + "03"], (seeds[reg + "14"]), scoring);
                Team winner7 = WprintMatchup(seeds[reg + "07"], (seeds[reg + "10"]), scoring);
                Team winner8 = WprintMatchup(seeds[reg + "02"], (seeds[reg + "15"]), scoring);

                Console.WriteLine(reg + ": Second round");
                Team winner21 = WprintMatchup(winner1, winner2, scoring);
                Team winner22 = WprintMatchup(winner3, winner4, scoring);
                Team winner23 = WprintMatchup(winner5, winner6, scoring);
                Team winner24 = WprintMatchup(winner7, winner8, scoring);

                Console.WriteLine(reg + ": Third round");
                Team winner31 = WprintMatchup(winner21, winner22, scoring);
                Team winner32 = WprintMatchup(winner23, winner24, scoring);

                Console.WriteLine(reg + ": Region Winner");
                ff.Add(WprintMatchup(winner31, winner32, scoring));
            }
            Console.WriteLine("Final Four");
            Team winner41 = WprintMatchup(ff[0], ff[1], scoring);
            Team winner42 = WprintMatchup(ff[2], ff[3], scoring);

            Console.WriteLine("Winner");
            WprintMatchup(winner41, winner42, scoring);
        }

        private static Team[][] WgenerateWinners(Dictionary<string, Team> seeds, bool adj)
        {
            Team[][] results = new Team[63][];
            string[] regions = new string[] { "W", "X", "Y", "Z" };
            for (int i = 0; i < regions.Length; i++)
            {
                string reg = regions[i];

                Team s16;
                if (!seeds.ContainsKey(reg + "16"))
                {
                    s16 = WsimulateMatchup(seeds[reg + "16a"], seeds[reg + "16b"], adj)[0];
                }
                else
                {
                    s16 = seeds[reg + "16"];
                }
                Team s11;
                if (!seeds.ContainsKey(reg + "11"))
                {
                    s11 = WsimulateMatchup(seeds[reg + "11a"], seeds[reg + "11b"], adj)[0];
                }
                else
                {
                    s11 = seeds[reg + "11"];
                }
                Team s12;
                if (!seeds.ContainsKey(reg + "12"))
                {
                    s12 = WsimulateMatchup(seeds[reg + "12a"], seeds[reg + "12b"], adj)[0];
                }
                else
                {
                    s12 = seeds[reg + "12"];
                }

                results[0 + (i * 15)] = WsimulateMatchup(seeds[reg + "01"], s16, adj);
                results[1 + (i * 15)] = WsimulateMatchup(seeds[reg + "08"], seeds[reg + "09"], adj);
                results[2 + (i * 15)] = WsimulateMatchup(seeds[reg + "05"], s12, adj);
                results[3 + (i * 15)] = WsimulateMatchup(seeds[reg + "04"], seeds[reg + "13"], adj);

                results[4 + (i * 15)] = WsimulateMatchup(seeds[reg + "06"], s11, adj);
                results[5 + (i * 15)] = WsimulateMatchup(seeds[reg + "03"], seeds[reg + "14"], adj);
                results[6 + (i * 15)] = WsimulateMatchup(seeds[reg + "07"], seeds[reg + "10"], adj);
                results[7 + (i * 15)] = WsimulateMatchup(seeds[reg + "02"], seeds[reg + "15"], adj);

                results[8 + (i * 15)] = WsimulateMatchup(results[0 + (i * 15)][0], results[1 + (i * 15)][0], adj);
                results[9 + (i * 15)] = WsimulateMatchup(results[2 + (i * 15)][0], results[3 + (i * 15)][0], adj);
                results[10 + (i * 15)] = WsimulateMatchup(results[4 + (i * 15)][0], results[5 + (i * 15)][0], adj);
                results[11 + (i * 15)] = WsimulateMatchup(results[6 + (i * 15)][0], results[7 + (i * 15)][0], adj);

                results[12 + (i * 15)] = WsimulateMatchup(results[8 + (i * 15)][0], results[9 + (i * 15)][0], adj);
                results[13 + (i * 15)] = WsimulateMatchup(results[10 + (i * 15)][0], results[11 + (i * 15)][0], adj);


                results[14 + (i * 15)] = WsimulateMatchup(results[12 + (i * 15)][0], results[13 + (i * 15)][0], adj);
            }
            results[60] = WsimulateMatchup(results[14][0], results[29][0], adj);
            results[61] = WsimulateMatchup(results[44][0], results[59][0], adj);

            results[62] = WsimulateMatchup(results[60][0], results[61][0], adj);
            return results;
        }

        private static Team[] WsimulateMatchup(Team team1, Team team2, bool adj)
        {
            double adjVal = .88;
            Team[] result = new Team[2];
            double score = Wscoring(team1, team2);
            if (adj)
            {
                if (score > .5)
                {
                    score = 1 - Math.Pow(1 - score, adjVal);
                }
                else if (score < .5)
                {
                    score = Math.Pow(score, adjVal);
                }
            }
            if (R.NextDouble() < score)
            {
                result[0] = team1;
                result[1] = team2;
            }
            else
            {
                result[0] = team2;
                result[1] = team1;
                score = 1 - score;
            }
            if (VERBOSE && false)
            {
                Console.WriteLine(string.Format("\t{0} defeats {1} ({2:0.0}%)", result[0].seedprint(), result[1].seedprint(), score * 100));
            }
            return result;
        }

        private static Team WprintMatchup(Team team1, Team team2, string scoring)
        {
            double score = Wscoring(team1, team2, scoring);
            if (score < .5)
            {
                Team temp = team1;
                team1 = team2;
                team2 = temp;
                score = 1 - score;
            }
            Console.WriteLine(string.Format("\t{0} defeats {1} ({2:0.0}%)", team1.seedprint(), team2.seedprint(), score * 100));
            return team1;
        }

    }


    internal class Team
    {
        public string name;
        internal string seed;

        public double[] rounds;
        public double[] otherWin;
        public string region;
        public int Wseed;

        public Team(string s, int r)
        {
            seed = s;
            rounds = new double[r];
            otherWin = new double[r];
        }

        //538
        public int meet(Team other, char gen)
        {
            HashSet<int>[] quads = new HashSet<int>[]{
                new HashSet<int>{ 1, 16, 8, 9 },
                new HashSet<int> { 5, 12, 4, 13 },
                new HashSet<int> { 6, 11, 3, 14 },
                new HashSet<int> { 7, 10, 2, 15 } };
            var left = Program.wleft;
            if (gen == 'W')
            {
                left = Program.wleft;
            }

            if (other.region.Equals(region))
            {
                if (Wseed + other.Wseed == 17)
                {
                    return 0;
                }

                int oBit = 0;
                int tBit = 0;
                for (int i = 0; i < quads.Length; i++)
                {
                    oBit <<= 1;
                    tBit <<= 1;
                    if (quads[i].Contains(other.Wseed))
                    {
                        oBit += 1;
                    }
                    if (quads[i].Contains(Wseed))
                    {
                        tBit += 1;
                    }
                }
                if ((oBit & tBit) != 0)
                {
                    return 1;
                }
                else if ((oBit | tBit) == 3 || (oBit | tBit) == 12)
                {
                    return 2;
                }
                return 3;

            }
            else if (left.Contains(other.region) ^ left.Contains(region))
            {
                return 5;
            }
            return 4;
        }

        internal string seedprint()
        {
            return string.Format("{0} {1}", seed[1] == '0' ? seed.Substring(2) : seed.Substring(1), name);
        }
    }
}
