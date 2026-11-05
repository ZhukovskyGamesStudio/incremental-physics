using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// An order sheet on the counter (DESIGN.md 2б, «Листки-заказы»): a sheet of paper with a sample lying on it. Picked
    /// up, it reads what the customer wants — a line of story, which changes nothing — the reward in coins, and a table
    /// with the weight to write in. The closer the answer, the more of the reward. A sheet sent is gone: no new one comes
    /// that day.
    public class OrderSheet : MonoBehaviour
    {
        public Transform seat;          // where its sample lies: the sample's home (it stays on the counter)
        public Transform leaf;          // the paper with the writing: it flies up before the eyes when picked up
        public Vector2 paperSize = new Vector2(0.225f, 0.3f);
        public Sample3D sample;
        [System.NonSerialized] public SheetView view;
        /// Today's order on it.
        [System.NonSerialized] public string story;
        /// The sample's shape, as the story has it (SampleShapes).
        [System.NonSerialized] public string shape = "cube";
        /// Who brings it (Persona): the trade the story speaks of — a driver, a baker, a granny, a fish…
        [System.NonSerialized] public string who = "driver";
        [System.NonSerialized] public int reward, number;
        [System.NonSerialized] public bool sent, stamped;
        /// One of the big contract's orders (Career); what the reward is made of («★★ +20% · научный сотрудник ×1,5»).
        [System.NonSerialized] public bool contract;
        [System.NonSerialized] public string note = "";
        /// What to find out (DESIGN.md 2б): "dyna" — the weight on the dynamometer, "bath" — the volume in the bath, "dens" —
        /// the density, worked out of the two; one, two or all three. And what has been written in for each, or null.
        [System.NonSerialized] public readonly List<string> tests = new List<string>();
        [System.NonSerialized] public string[] answers = new string[2];

        public bool Answered { get { for (int i = 0; i < tests.Count; i++) if (!Filled(i)) return false; return tests.Count > 0; } }
        /// Row i has an answer: a number, or a box ticked (the odd measurements, Oddity).
        public bool Filled(int i) => Oddity.Is(tests[i]) ? Oddity.Ticked(answers[i]) != 0 : Value(i) > 0;
        /// The number written in row i, or -1.
        public double Value(int i) => i < answers.Length && !string.IsNullOrEmpty(answers[i]) &&
            double.TryParse(answers[i].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : -1;
        /// The row's name and unit, as the table writes it.
        public static string RowName(string test) => Oddity.Is(test) ? Oddity.RowName(test) : test switch { "bath" => "Объём, л", "dens" => "Плотность, кг/л", "angle" => "Угол скольжения, °", "mu" => "Трение μ", _ => "Вес, Н" };

        /// The sample is lying on its sheet: the order can be sent.
        public bool SampleHome => sample.At == Sample3D.Where.Crate && sample.transform.parent == seat;

        static readonly string[] Stories =
        {
            "cube|driver|Хотим перевезти {0} таких образцов в грузовике. Сколько весит один?",
            "brick|engineer|Собираем мост из этих брусков. Инженер просит вес каждого.",
            "cube|cosmonaut|Отправляем образец на Луну. Бухгалтерии нужен его вес здесь, на Земле.",
            "brick|librarian|Ставим {0} таких на полку в библиотеке. Выдержит ли она? Начнём с веса одного.",
            "cylinder|baker|Пекарне нужна гиря для весов. Подойдёт эта? Сколько она весит?",
            "bracelet|granny|Нашла это на чердаке. Уверена, что это золото. Взвесьте, пожалуйста.",
            "ball|kid|Будем запускать из рогатки. Чтобы рассчитать выстрел, нужен вес.",
            "cylinder|mechanic|Строим лифт на {1} этажей, это противовес. Сколько он весит?",
            "brick|postman|Почта берёт за посылку по весу. Сколько заплатим за этот кирпичик?",
            "ball|strongman|Цирковой силач обещает поднять {0} таких. Проверьте, не жульничает ли он.",
            "bell|mechanic|Вешаем колокольчик над крыльцом. Крючок выдержит, если знать вес.",
            "stone|fish|Кладём камни в аквариум. Не треснет ли дно? Начнём с веса одного камня.",
            "cylinder|teacher|Для школьного опыта нужен груз известного веса. Этот подойдёт?",
            "cube|driver|Отправляем {0} образцов поездом. Вагону важен общий вес — начнём с одного.",
            "cube|robot|Наш дрон должен поднять этот образец. Сколько он весит?",
            "vase|curator|Музей просит паспорт экспоната: размеры уже есть, не хватает веса.",
            "cylinder|blacksmith|Кузнец спорит с мельником, чья гиря тяжелее. Рассудите: сколько весит эта?",
            "stone|sailor|Это якорь для игрушечной лодки. Не утянет ли он её на дно? Нужен вес.",
            "bottle|sailor|Выловили бутылку с запиской. Сколько она весит вместе с письмом?",
            "pear|baker|Пеку пирог с грушами. Сколько весит такая груша?",
            "goblet|strongman|Мой кубок чемпиона! Сколько он весит?",
            "egg|duck|Снесла яйцо. Сколько оно весит?",
        };

        /// A new order on the sheet: what to find out (of what the lab can), a story, a reward of 20–35 coins, more for
        /// more rows and for the density.
        public void Deal(int n, bool bath, bool dens, bool incline = false, List<string> odd = null, string wantWho = null)
        {
            number = n;
            tests.Clear();
            float r = Random.value;
            if (dens)
            {
                if (r < 0.2f) tests.Add("dyna");
                else if (r < 0.35f) tests.Add("bath");
                else if (r < 0.55f) { tests.Add("dyna"); tests.Add("bath"); }
                else if (r < 0.8f) tests.Add("dens");
                else { tests.Add("dyna"); tests.Add("bath"); tests.Add("dens"); }
            }
            else if (bath)
            {
                if (r < 0.35f) tests.Add("dyna");
                else if (r < 0.65f) tests.Add("bath");
                else { tests.Add("dyna"); tests.Add("bath"); }
            }
            else tests.Add("dyna");
            // with the inclined plane, a third of the orders are for it: the angle of sliding, the friction, or both
            if (incline && (forceSlope || Random.value < 0.35f))
            {
                tests.Clear();
                float q = Random.value;
                if (q < 0.5f) tests.Add("angle");
                else if (q < 0.8f) tests.Add("mu");
                else { tests.Add("angle"); tests.Add("mu"); }
            }
            // with odd measurements in the lab (DESIGN.md 2б, «Крейзи-измерения»), nearly half the orders are odd — one odd
            // question, the one its story asks (a musician wants the note and nothing else); the newest department a
            // little more often
            string forced = Forced(odd);
            bool slope = tests.Contains("angle") || tests.Contains("mu");
            if (forced != null || (odd != null && odd.Count > 0 && !slope && Random.value < 0.45f))
            {
                tests.Clear();
                tests.Add(forced ?? (Random.value < 0.35f ? odd[odd.Count - 1] : odd[Random.Range(0, odd.Count)]));
            }
            answers = new string[tests.Count];
            // the story asks for exactly what the table asks: every set of questions has its own stories
            var pool = Pool(string.Join("+", tests));
            if (wantWho != null)
            {   // a contract's order: its customer's own stories, if he has one for these questions
                var mine = System.Array.FindAll(pool, x => x.Split('|')[1] == wantWho);
                if (mine.Length > 0) pool = mine;
            }
            // no story twice in a day, and a customer who has been today comes again only when no one else can ask this
            var fresh = System.Array.FindAll(pool, x => !_toldToday.Contains(x) && !_cameToday.Contains(x.Split('|')[1]));
            if (fresh.Length == 0) fresh = System.Array.FindAll(pool, x => !_toldToday.Contains(x));
            if (fresh.Length > 0) pool = fresh;
            // a story tells what the sample is, and the sample comes in that shape: a stone, a weight, an ingot…
            var line = pool[Random.Range(0, pool.Length)];
            _toldToday.Add(line);
            var told = line.Split('|');
            _cameToday.Add(told[1]);
            shape = told[0];
            who = told[1];
            story = string.Format(told[2], Random.Range(2, 50) * 10, Random.Range(3, 25));
            // the density is three jobs in one (weigh, sink, divide): it pays like them
            reward = Mathf.RoundToInt(Random.Range(20, 36) * (tests.Count == 3 ? 2.6f : tests.Count == 2 ? 1.8f : 1)
                                     * (tests.Contains("dens") ? (tests.Count == 1 ? 2.6f : 1.6f) : tests.Contains("mu") ? 1.4f : 1)
                                     * Oddity.Work(tests));
            sent = stamped = false;
        }

        static readonly HashSet<string> _toldToday = new HashSet<string>();
        static readonly HashSet<string> _cameToday = new HashSet<string>();
        /// A new day: every story may be told again.
        public static void NewDay() { _toldToday.Clear(); _cameToday.Clear(); }

        /// The check makes every order one for the inclined plane (to see it worked).
        public static bool forceSlope;
        /// The check's odd orders, one after another, to see every odd department worked (taken as the orders come).
        public static readonly List<string> forceQueue = new List<string>();

        static string Forced(List<string> odd)
        {
            while (forceQueue.Count > 0)
            {
                var t = forceQueue[0];
                forceQueue.RemoveAt(0);
                if (odd != null && odd.Contains(t)) return t;
            }
            return null;
        }

        /// The stories of the odd orders: who wants to know what such a thing tastes like, what bites on it…
        static string[] OddStories(string test) => test switch
        {
            "taste" => new[]
            {
                "cube|cook|Варю суп из метеоритов. Какой вкус у этого образца?",
                "ball|kid|Мама говорит, это есть нельзя. А какое оно на вкус?",
                "stone|gnome|Гномы едят камни. Вкусный ли этот?",
                "pear|cook|Груша для десерта. Нужен точный вкус — вплоть до умами.",
                "cylinder|robot|У меня нет языка. Каков этот предмет на вкус?",
                "egg|duck|Это не моё яйцо. Каким оно было бы на вкус?",
            },
            "fish" => new[]
            {
                "ball|fisherman|Новая блесна. Кто на неё клюнет?",
                "bell|fisherman|Колокольчик для донки. Кого он приманит?",
                "stone|fish|Сосед подложил это в мой аквариум. Кто на это клюёт?",
                "egg|duck|Мои утята боятся щуки. Клюнет ли она на такое?",
                "cube|kid|Летом еду на рыбалку. Кто клюнет на этот кубик?",
            },
            "glow" => new[]
            {
                "stone|cosmonaut|Камень с Луны. Светится ли он в темноте?",
                "ball|kid|Хочу ночник для спальни. Этот шарик светится?",
                "ring|jeweler|Говорят, кольцо светится по ночам. Проверьте.",
                "bottle|sailor|Бутылка для маяка. Видно ли её в темноте?",
            },
            "mirror" => new[]
            {
                "ring|vampire|Ищу кольцо, которого не видно в зеркале. Это подходит?",
                "goblet|vampire|Кубок для гостей замка. Отражается ли он в зеркале?",
                "vase|curator|Экспонат из замка графа. Отражается ли он в зеркале?",
                "bracelet|granny|Браслет, говорят, заколдован. Отражается ли он?",
            },
            "cat" => new[]
            {
                "ball|granny|Ищу игрушку для Мурзика. Как он отнесётся к этой?",
                "cube|kid|Хочу подарить это коту. Он обрадуется?",
                "stone|teacher|Наш школьный кот капризный. Что он скажет об этом?",
                "egg|duck|Кот во дворе пугает утят. Что он сделает с этим яйцом?",
            },
            "note" => new[]
            {
                "bell|musician|Колокольчик для оркестра. Какую ноту он поёт?",
                "goblet|musician|Играю на стеклянных кубках. Какая нота у этого?",
                "cylinder|teacher|Нужен камертон для урока пения. Какая у этого нота?",
                "bottle|sailor|Делаю рынду из бутылки. Какую ноту она даст?",
            },
            "tickle" => new[]
            {
                "ball|clown|Реквизит для цирка. Боится ли он щекотки?",
                "egg|duck|Моё яйцо всё время хихикает. Оно боится щекотки?",
                "cube|kid|Мой кубик смешливый? Проверьте пёрышком.",
                "pear|clown|Груша для фокуса. Смеётся ли она от щекотки?",
            },
            _ => Stories,
        };

        /// The stories for a set of questions ("dyna+bath"): each asks for exactly those.
        static string[] Pool(string key) => key switch
        {
            "bath" => BathStories,
            "dens" => DensStories,
            "dyna+bath" => WeightVolumeStories,
            "dyna+bath+dens" => AllThreeStories,
            "angle" => AngleStories,
            "mu" => MuStories,
            "angle+mu" => SlopeBothStories,
            _ => Oddity.Is(key) ? OddStories(key) : Stories,
        };

        /// Orders for the inclined plane: things that lie flat and slide (not those that roll).
        static readonly string[] AngleStories =
        {
            "brick|mechanic|Делаем горку на складе. С какого угла этот ящик поедет сам?",
            "cube|kid|Хочу съехать с горки на таком. Какой нужен наклон?",
            "stone|engineer|Строим насыпь. Под каким углом этот камень начнёт съезжать?",
            "ingot|driver|Кузов самосвала поднимается — когда груз поедет? Нужен угол.",
        };

        static readonly string[] MuStories =
        {
            "brick|beaver|Тащу такое по мокрому склону. Какое тут трение?",
            "bell|teacher|Для урока физики нужен коэффициент трения этого образца.",
            "cube|robot|Мои колёса буксуют. Узнайте трение вот этого.",
        };

        static readonly string[] SlopeBothStories =
        {
            "brick|engineer|Проектируем пандус. Нужны угол, с которого груз поедет, и его трение.",
            "ingot|mechanic|Чиним конвейер-горку: при каком угле слиток поедет и какое у него трение?",
        };

        static readonly string[] WeightVolumeStories =
        {
            "brick|postman|Посылку берут по весу и по размеру. Сколько весит этот брусок и какой у него объём?",
            "cube|driver|Грузим кузов: важны и вес, и сколько места займёт. Узнайте оба.",
            "stone|fish|Кладу камень в аквариум: выдержит ли дно и не перельётся ли вода? Нужны вес и объём.",
        };

        static readonly string[] AllThreeStories =
        {
            "ingot|gnome|Мне продали слиток «золота». Взвесьте, измерьте объём и посчитайте плотность.",
            "ring|jeweler|Полная экспертиза кольца: вес, объём и плотность.",
            "goblet|curator|Паспорт экспоната: вес, объём и плотность материала.",
        };

        static readonly string[] BathStories =
        {
            "cube|tailor|Шьём чехол точно по образцу. Сколько места он занимает?",
            "brick|engineer|Заливаем такие в бетон. Сколько бетона они вытеснят? Нужен объём.",
            "cube|postman|Упаковываем {0} штук в коробки. Узнайте объём одной.",
            "cylinder|kid|Эта деталь должна влезть в бутылку. Какой у неё объём?",
            "stone|fish|Аквариумист боится перелить воду через край. Сколько воды вытеснит этот камень?",
            "vase|granny|Ставлю вазу в сервант. Сколько места она займёт? Нужен объём.",
            "bell|teacher|Школьный звонок упаковываем в коробку. Какой у него объём?",
        };

        static readonly string[] DensStories =
        {
            "goblet|gnome|Говорят, этот кубок золотой. Из чего он на самом деле? Нужна плотность.",
            "brick|beaver|Поплывёт ли такой плот? Узнайте плотность материала.",
            "stone|teacher|Два куска одинаковые на вид, а весят по-разному. Какая у этого плотность?",
            "ring|jeweler|Подозреваю подделку. Проверьте плотность этого кольца.",
            "bracelet|jeweler|Браслет «из чистого серебра». Так ли это? Нужна плотность.",
            "ball|duck|Делаем буёк для озера. Нужна плотность — утонет он или нет?",
        };
    }
}
