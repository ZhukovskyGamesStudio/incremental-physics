using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics
{
    /// The lab's goals between the days (DESIGN.md 2б, «Цели между днями»), kept in the save.
    [Serializable]
    public class CareerData
    {
        // the academy's grant: a goal a day, five goals met — a premium
        public int grantSteps, grantLevel;
        public int goalKind = -1, goalTarget, goalDay = -1, todayGood, todayDelivered, todayCoins;
        public bool goalDone;
        public int lastDayCoins;
        // the regular customers: trust points by trade
        public List<string> trustIds = new List<string>();
        public List<int> trustPts = new List<int>();
        public List<string> gifted = new List<string>();
        public int delivered;
        // the big contract
        public string contract = "";
        public int contractNeed, contractGot, contractEnds, contractReward, contractsDone, contractNext;
        // the rival and the rank
        public bool rivalOn;
        public int weekStart, weekCoins, rivalTarget, weeksWon;
        // what happened, for the map (cleared when the next day starts); what has opened
        public List<string> news = new List<string>();
        public List<string> opened = new List<string>();
        // the certificates earned (Awards), where each hangs
        public List<AwardData> awards = new List<AwardData>();
    }

    /// The four goals between the days (DESIGN.md 2б, «Цели между днями»). They come one after another, not all at once:
    /// the academy's grant from the second day; the regular customers once ten orders are delivered; the big contract
    /// once the first grant is paid; the rival and the rank once the first contract is done.
    public static class Career
    {
        /// The headless check runs it all faster (a grant of two goals, a week of two days…), to see it all in a few days.
        public static bool Fast;
        static int GrantSize => Fast ? 2 : 5;
        static int WeekDays => Fast ? 2 : 5;
        static int RegularsAfter => Fast ? 4 : 10;
        public const double ContractAccuracy = 0.75;

        public static bool GrantOpen(GameState g) => g.Lessons >= 1;
        public static bool RegularsOpen(GameState g) => g.Career.delivered >= RegularsAfter;
        public static bool ContractOpen(GameState g) => g.Career.grantLevel >= 1;
        public static bool RivalOpen(GameState g) => g.Career.contractsDone >= 1;
        public static bool ContractActive(GameState g) => !string.IsNullOrEmpty(g.Career.contract);

        // ------------------------------------------------------------------ the regular customers

        public static int Trust(GameState g, string who)
        {
            int i = g.Career.trustIds.IndexOf(who);
            return i >= 0 ? g.Career.trustPts[i] : 0;
        }

        /// Stars: one at a point of trust, two at three, three at six.
        public static int Stars(GameState g, string who)
        {
            if (!RegularsOpen(g)) return 0;
            int p = Trust(g, who);
            return p >= 6 ? 3 : p >= 3 ? 2 : p >= 1 ? 1 : 0;
        }

        public static string StarsText(int n) => n <= 0 ? "" : new string(GameState.Star[0], n);

        static void AddTrust(GameState g, string who, int d)
        {
            var c = g.Career;
            int i = c.trustIds.IndexOf(who);
            if (i < 0) { c.trustIds.Add(who); c.trustPts.Add(0); i = c.trustIds.Count - 1; }
            c.trustPts[i] = Math.Max(0, Math.Min(9, c.trustPts[i] + d));
        }

        /// The order's reward, with what the player has earned on it: +10% a star of its customer, times the rank.
        public static double RewardMult(GameState g, string who) => (1 + 0.1 * Stars(g, who)) * g.RankMult;

        /// What the reward is made of, as the sheet writes it under the reward («★★ +20% · научный сотрудник ×1,5»).
        public static string RewardNote(GameState g, string who)
        {
            var parts = new List<string>();
            int s = Stars(g, who);
            if (s > 0) parts.Add(StarsText(s) + " +" + s * 10 + "%");
            if (g.RankPts > 0) parts.Add(g.RankName + " ×" + g.RankMult.ToString("0.##"));
            return string.Join("  ·  ", parts);
        }

        // ------------------------------------------------------------------ the contracts

        /// The contracts, one after another: who brings its orders, its name.
        static readonly (string who, string name, string what)[] Contracts =
        {
            ("teacher", "Школа", "кабинет физики собирает образцы"),
            ("kid", "Кружок юных физиков", "опыты для ребят"),
            ("engineer", "Стройка моста", "проверка материалов"),
            ("driver", "Автобаза", "грузы перед рейсом"),
            ("mechanic", "Мастерская", "детали на проверку"),
        };

        public static string ContractWho(GameState g) => ContractActive(g) ? Contracts[ContractIndex(g)].who : null;
        static int ContractIndex(GameState g) => Math.Max(0, Array.FindIndex(Contracts, x => x.name == g.Career.contract));
        public static string ContractName(GameState g) => g.Career.contract;

        // ------------------------------------------------------------------ the day

        /// The morning: today's goal is set, what has newly opened is told (title, line, note — the day shows them on
        /// cards), a contract is offered, the rival's week begins.
        public static List<(string title, string sum, string note)> Morning(GameState g, int ordersToday)
        {
            var c = g.Career;
            var cards = new List<(string, string, string)>();
            c.news.Clear();
            c.todayGood = c.todayDelivered = c.todayCoins = 0;
            if (GrantOpen(g) && c.goalDay != g.Lessons)
            {
                c.goalDay = g.Lessons;
                c.goalDone = false;
                c.goalKind = g.Lessons % 3;
                c.goalTarget = c.goalKind switch
                {
                    0 => Math.Max(40, (int)Math.Round(c.lastDayCoins * 1.1 / 5) * 5),
                    1 => Math.Max(2, ordersToday - 1),
                    _ => ordersToday,
                };
                if (Open(g, "grant")) cards.Add(("Грант академии", "", $"каждый день — цель; {GrantSize} целей — премия"));
            }
            if (RegularsOpen(g) && Open(g, "regulars"))
                cards.Add(("Постоянные заказчики", "", "точные ответы — звёзды заказчику; звезда +10% к его заказам"));
            if (ContractOpen(g) && !ContractActive(g) && g.Lessons >= c.contractNext)
            {
                var (who, name, what) = Contracts[c.contractsDone % Contracts.Length];
                c.contract = name;
                c.contractNeed = Fast ? 2 : Math.Min(7, 3 + c.contractsDone);
                c.contractGot = 0;
                int days = Fast ? 2 : c.contractNeed / 2 + 2;
                c.contractEnds = g.Lessons + days;   // through day number contractEnds
                c.contractReward = 50 * c.contractNeed;
                Open(g, "contract");
                cards.Add(($"Контракт «{name}»", $"{c.contractNeed} заказов: {Lab3D.Persona.Name(who)} за {days} {Days(days)}",
                           $"{what}; точность от {ContractAccuracy * 100:0}% · награда {c.contractReward} {GameState.Coin}"));
            }
            if (RivalOpen(g) && !c.rivalOn)
            {
                c.rivalOn = true;
                c.weekStart = g.Lessons;
                c.weekCoins = 0;
                c.rivalTarget = Math.Max(150, (int)Math.Round(Math.Max(c.lastDayCoins, 40) * WeekDays * 1.1 / 10) * 10);
                Open(g, "rival");
                cards.Add(("Соперник", "лаборатория профессора Пробиркина", $"кто больше заработает за {WeekDays} {Days(WeekDays)}? победа — повышение звания"));
            }
            g.Save();
            return cards;
        }

        static string Days(int n) => n % 10 == 1 && n % 100 != 11 ? "день" : n % 10 is >= 2 and <= 4 && (n % 100 < 12 || n % 100 > 14) ? "дня" : "дней";

        static bool Open(GameState g, string what)
        {
            if (g.Career.opened.Contains(what)) return false;
            g.Career.opened.Add(what);
            return true;
        }

        /// A customer went off without its order, tired of waiting: a regular trusts the lab a point less.
        public static void Lost(GameState g, string who)
        {
            if (RegularsOpen(g)) AddTrust(g, who, -1);
            g.Career.news.Add($"Не дождался и ушёл: {Lab3D.Persona.Name(who)}");
        }

        /// An order is delivered: trust, the contract, the week, today's goal. Returns what to tell on a card, or null.
        public static (string title, string sum, string note)? Delivered(GameState g, string who, double accuracy, int coins, bool contractOrder)
        {
            var c = g.Career;
            c.delivered++;
            c.todayDelivered++;
            c.todayCoins += coins;
            if (accuracy >= 0.8) c.todayGood++;
            if (c.rivalOn) c.weekCoins += coins;
            (string, string, string)? card = null;
            if (RegularsOpen(g))
            {
                int was = Stars(g, who);
                AddTrust(g, who, accuracy >= 0.8 ? 1 : accuracy < 0.4 ? -1 : 0);
                int now = Stars(g, who);
                if (now > was)
                {
                    card = ($"{Lab3D.Persona.Name(who)} доверяет вам", StarsText(now), $"+{now * 10}% к его заказам");
                    if (now == 3 && !c.gifted.Contains(who))
                    {   // a regular of three stars brings a present once
                        c.gifted.Add(who);
                        g.Earn(Cur.Coin, 30);
                        card = ($"{Lab3D.Persona.Name(who)}: «спасибо за всё!»", StarsText(3) + "  +30 " + GameState.Coin, "подарок постоянного заказчика");
                    }
                }
            }
            if (contractOrder && ContractActive(g))
            {
                if (accuracy >= ContractAccuracy)
                {
                    c.contractGot++;
                    if (c.contractGot >= c.contractNeed)
                    {
                        g.Earn(Cur.Coin, c.contractReward);
                        c.news.Add($"Контракт «{c.contract}» выполнен: +{c.contractReward} {GameState.Coin}");
                        card = ($"Контракт «{c.contract}» выполнен", "+" + c.contractReward + " " + GameState.Coin, "");
                        c.contract = "";
                        c.contractsDone++;
                        c.contractNext = g.Lessons + 2;   // the next one the day after tomorrow
                    }
                    else card = ($"Контракт «{c.contract}»: {c.contractGot}/{c.contractNeed}", "", "");
                }
                else card = ($"Контракт: не засчитано", "", $"нужна точность от {ContractAccuracy * 100:0}%, было {accuracy * 100:0}%");
            }
            var goal = CheckGoal(g, false);
            if (goal.HasValue) card = goal;
            g.Save();
            return card;
        }

        /// Is today's goal met? Once met it counts once: a step of the grant (and the premium at the last step).
        public static (string title, string sum, string note)? CheckGoal(GameState g, bool allDone)
        {
            var c = g.Career;
            if (!GrantOpen(g) || c.goalDone || c.goalDay != g.Lessons) return null;
            bool met = c.goalKind switch
            {
                0 => c.todayCoins >= c.goalTarget,
                1 => c.todayGood >= c.goalTarget,
                _ => allDone && c.todayDelivered >= c.goalTarget,
            };
            if (!met) return null;
            c.goalDone = true;
            c.grantSteps++;
            if (c.grantSteps >= GrantSize)
            {
                int premium = 50 * (c.grantLevel + 2);
                c.grantSteps = 0;
                c.grantLevel++;
                g.Earn(Cur.Coin, premium);
                c.news.Add($"Премия академии: +{premium} {GameState.Coin}");
                return ("Цель дня выполнена", $"премия академии +{premium} {GameState.Coin}", "грант начинается заново");
            }
            c.news.Add($"Цель дня выполнена: грант {c.grantSteps}/{GrantSize}");
            return ("Цель дня выполнена", "", $"грант академии {c.grantSteps}/{GrantSize}");
        }

        /// The day ends (before it is counted): the goal of «all the orders before six» (`allDone`: all sent before the evening),
        /// a contract's deadline, the rival's week.
        public static void DayEnd(GameState g, bool allDone, int dayCoins)
        {
            var c = g.Career;
            CheckGoal(g, allDone);
            if (GrantOpen(g) && c.goalDay == g.Lessons && !c.goalDone) c.news.Add("Цель дня не выполнена — грант ждёт");
            c.lastDayCoins = c.todayCoins;   // the orders' coins only: a premium is not what a day earns by itself
            int day = g.Lessons + 1;   // the day that is ending
            if (ContractActive(g) && day >= c.contractEnds && c.contractGot < c.contractNeed)
            {
                c.news.Add($"Контракт «{c.contract}» сорван: {c.contractGot}/{c.contractNeed}");
                c.contract = "";
                c.contractNext = day;   // another one tomorrow
            }
            if (c.rivalOn && day - c.weekStart >= WeekDays)
            {
                if (c.weekCoins >= c.rivalTarget)
                {
                    int next = Math.Min(GameState.RankFrom.Length - 1, g.Rank + 1);
                    g.RankPts = GameState.RankFrom[next];
                    c.weeksWon++;
                    c.news.Add($"Неделя за вами: {c.weekCoins} против {c.rivalTarget} {GameState.Coin}. Повышение: {g.RankName} ×{g.RankMult:0.##}");
                }
                else c.news.Add($"Пробиркин обогнал: {c.rivalTarget} против ваших {c.weekCoins} {GameState.Coin}");
                c.rivalTarget = (int)Math.Round(Math.Max(c.weekCoins * 1.15, c.rivalTarget * 1.05) / 10) * 10;
                c.weekCoins = 0;
                c.weekStart = day;
            }
        }

        // ------------------------------------------------------------------ what is written

        public static string GoalText(GameState g)
        {
            var c = g.Career;
            return c.goalKind switch
            {
                0 => $"заработать {c.goalTarget} {GameState.Coin}",
                1 => $"{c.goalTarget} {Orders(c.goalTarget)} с точностью от 80%",
                _ => "все заказы — до конца рабочего дня",
            };
        }

        static string Orders(int n) => n % 10 == 1 && n % 100 != 11 ? "заказ" : n % 10 is >= 2 and <= 4 && (n % 100 < 12 || n % 100 > 14) ? "заказа" : "заказов";

        static string GoalProgress(GameState g, int ordersToday)
        {
            var c = g.Career;
            if (c.goalDone) return "выполнена";
            return c.goalKind switch
            {
                0 => $"{c.todayCoins}/{c.goalTarget}",
                1 => $"{c.todayGood}/{c.goalTarget}",
                _ => $"{c.todayDelivered}/{ordersToday}",
            };
        }

        /// The lines in the corner of the day: today's goal, the contract, the week.
        public static List<string> DayLines(GameState g, int ordersToday)
        {
            var c = g.Career;
            var l = new List<string>();
            if (GrantOpen(g) && c.goalDay == g.Lessons) l.Add($"Цель дня: {GoalText(g)} — {GoalProgress(g, ordersToday)}");
            if (ContractActive(g)) l.Add($"Контракт «{c.contract}»: {c.contractGot}/{c.contractNeed} · последний день {c.contractEnds}");
            if (c.rivalOn) l.Add($"Неделя: вы {c.weekCoins} {GameState.Coin} · Пробиркин {c.rivalTarget} {GameState.Coin}");
            return l;
        }

        /// The board on the map: what happened yesterday, then where every goal stands.
        public static List<(string text, bool fresh)> MapLines(GameState g)
        {
            var c = g.Career;
            var l = new List<(string, bool)>();
            foreach (var n in c.news) l.Add((n, true));
            if (GrantOpen(g)) l.Add(($"Грант академии: {c.grantSteps}/{GrantSize} · премия {50 * (c.grantLevel + 2)} {GameState.Coin}", false));
            if (ContractActive(g)) l.Add(($"Контракт «{c.contract}»: {c.contractGot}/{c.contractNeed} до дня {c.contractEnds} · {c.contractReward} {GameState.Coin}", false));
            if (c.rivalOn) l.Add(($"Соперник: неделя {g.Lessons - c.weekStart}/{WeekDays} · вы {c.weekCoins} из {c.rivalTarget} {GameState.Coin}", false));
            return l;
        }
    }
}
