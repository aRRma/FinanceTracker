#!/usr/bin/env dotnet
#:property PublishAot=false

// Подбор яркой палитры цветов счёта перебором в OKLCH.
// dotnet tasks/account-distinction/tools/palette.cs
//
// Метод — из скилла dataviz (категориальная палитра): цвет держит полосу светлоты
// OKLCH (светлая тема 0,43–0,77, тёмная 0,48–0,67) и насыщенность не ниже 0,10, иначе
// читается серым; пары различаются ΔE в OKLab ×100: обычным зрением не меньше 15,
// при дейтеранопии и протанопии (Machado 2009, полная степень) — цель 8, минимум 6
// при втором признаке. У нас второй признак есть всегда — название и значок счёта.
// Счета стоят на балансах все сразу, поэтому пары считаются все, а не соседние.
//
// Для каждого тона берётся самая насыщенная светлота в полосе, которая ещё влезает
// в sRGB; дальше перебираются наборы тонов и их порядок. Значок на заливке пишется
// белым или тёмным — что контрастнее; оба варианта печатаются.

using System.Globalization;

(string Key, string Name, double Hue)[] hues =
[
    ("red", "красный", 27), ("coral", "коралловый", 40), ("orange", "оранжевый", 55), ("amber", "янтарный", 75),
    ("yellow", "жёлтый", 95), ("lime", "салатовый", 125), ("green", "зелёный", 148), ("teal", "бирюзовый", 180),
    ("cyan", "голубой", 215), ("blue", "синий", 258), ("indigo", "индиго", 278), ("violet", "фиолетовый", 300),
    ("magenta", "пурпурный", 330), ("pink", "розовый", 355),
];

var inv = CultureInfo.InvariantCulture;
string mode = args.Length > 0 ? args[0] : "search";

// Одна светлота на тему и потолок насыщенности 0,19: цвета одного веса читаются семьёй,
// а предельная насыщенность края sRGB даёт кислоту (пурпурный выходил неоновым). Светлая
// тема — 0,64: ярко и белый значок ещё держит контраст; тёмная — 0,68: цвет светится на тёмном.
var light = hues.ToDictionary(h => h.Key, h => Best(h.Hue, 0.64, 0.64));
var dark = hues.ToDictionary(h => h.Key, h => Best(h.Hue, 0.68, 0.68));

if (mode == "list")
{
    Console.WriteLine("тон          светлая                      тёмная");
    foreach (var h in hues)
    {
        Console.WriteLine(string.Create(inv, $"{h.Name,-12} {Describe(light[h.Key], "#FFFFFF")}   {Describe(dark[h.Key], "#1C2124")}"));
    }

    return 0;
}

if (mode == "hex")
{
    // Готовая палитра: «имя=светлый/тёмный» через запятую. Очередь строится жадно — следующим
    // ставится цвет, самый далёкий от уже стоящих (худшее из обычного зрения и дальтонизма,
    // приведённых к своим порогам 15 и 8); значок — белый или тёмный, что контрастнее
    var items = args[1].Split(',').Select(s => s.Split('=', '/')).Select(p => (Name: p[0], L: p[1], D: p[2])).ToList();
    double Score((string Name, string L, string D) a, (string Name, string L, string D) b) =>
        Math.Min(Math.Min(DeltaE(a.L, b.L, null), DeltaE(a.D, b.D, null)) / 15,
            new[] { Deutan, Protan }.SelectMany(f => new[] { DeltaE(a.L, b.L, f), DeltaE(a.D, b.D, f) }).Min() / 8);
    var order = new List<(string Name, string L, string D)> { items.First(i => i.Name == (args.Length > 2 ? args[2] : items[0].Name)) };
    while (order.Count < items.Count)
        order.Add(items.Where(i => !order.Contains(i)).MaxBy(i => order.Min(o => Score(o, i))));
    Console.WriteLine("очередь и значок на заливке:");
    foreach (var c in order)
    {
        string gl = Contrast(c.L, "#FFFFFF") >= Contrast(c.L, "#16191C") ? "белый" : "тёмный";
        string gd = Contrast(c.D, "#FFFFFF") >= Contrast(c.D, "#16191C") ? "белый" : "тёмный";
        Console.WriteLine(string.Create(inv, $"{c.Name,-11} {c.L} значок {gl} {Math.Max(Contrast(c.L, "#FFFFFF"), Contrast(c.L, "#16191C")):0.0}   {c.D} значок {gd} {Math.Max(Contrast(c.D, "#FFFFFF"), Contrast(c.D, "#16191C")):0.0}"));
    }

    Console.WriteLine("пары обычным зрением ближе 15 и при дальтонизме ближе 8 (номера — места в очереди):");
    for (int i = 0; i < order.Count; i++)
        for (int j = i + 1; j < order.Count; j++)
        {
            double nrm = Math.Min(DeltaE(order[i].L, order[j].L, null), DeltaE(order[i].D, order[j].D, null));
            double cvd = new[] { Deutan, Protan }.SelectMany(f => new[] { DeltaE(order[i].L, order[j].L, f), DeltaE(order[i].D, order[j].D, f) }).Min();
            if (nrm < 15 || cvd < 8)
                Console.WriteLine(string.Create(inv, $"  {i + 1}.{order[i].Name} — {j + 1}.{order[j].Name}: обычное {nrm:0.0}, дальтонизм {cvd:0.0}"));
        }

    return 0;
}

if (mode == "check")
{
    // Ручная проверка набора: «тон:ступень» через запятую, в порядке очереди
    double[] lt = [0.56, 0.64, 0.74], dt = [0.52, 0.60, 0.665];
    var set = args[1].Split(',').Select(s => s.Split(':')).Select(p => (Key: p[0], Tier: int.Parse(p[1], inv))).ToArray();
    var cols = set.Select(s => { var h = hues.First(x => x.Key == s.Key); return (h.Key, h.Name, L: Best(h.Hue, lt[s.Tier], lt[s.Tier]), D: Best(h.Hue, dt[s.Tier], dt[s.Tier])); }).ToArray();
    double worst = double.MaxValue, worstCvd4 = double.MaxValue;
    string worstPair = "";
    for (int i = 0; i < cols.Length; i++)
        for (int j = i + 1; j < cols.Length; j++)
        {
            double d = Math.Min(DeltaE(cols[i].L.Hex, cols[j].L.Hex, null), DeltaE(cols[i].D.Hex, cols[j].D.Hex, null));
            if (d < worst) { worst = d; worstPair = $"{cols[i].Name} — {cols[j].Name}"; }
            if (j < 4)
                worstCvd4 = Math.Min(worstCvd4, new[] { Deutan, Protan }.SelectMany(f => new[] { DeltaE(cols[i].L.Hex, cols[j].L.Hex, f), DeltaE(cols[i].D.Hex, cols[j].D.Hex, f) }).Min());
        }

    Console.WriteLine(string.Create(inv, $"худшая пара обычным зрением {worst:0.0} ({worstPair}); дальтонизм среди первых четырёх {worstCvd4:0.0}"));
    foreach (var c in cols)
        Console.WriteLine(string.Create(inv, $"{c.Name,-12} {Describe(c.L, "#FFFFFF")}   {Describe(c.D, "#1C2124")}"));
    return 0;
}

if (mode == "tiers")
{
    // Тон и ступень светлоты подбираются вместе: при одной светлоте у всех различие держит
    // только тон, и восемь цветов не расходятся дальше ΔE 9. Ступени — тёмная, средняя,
    // светлая в каждой теме; поиск в глубину с отсечением по худшей паре.
    int count = args.Length > 1 ? int.Parse(args[1], inv) : 8;
    string[] skip = args.Length > 2 ? args[2].Split(',') : [];
    double[] lightTiers = [0.56, 0.64, 0.74], darkTiers = [0.52, 0.60, 0.665];
    var cands = (from h in hues where !skip.Contains(h.Key)
                 from t in Enumerable.Range(0, 3)
                 let l = Best(h.Hue, lightTiers[t], lightTiers[t])
                 let d = Best(h.Hue, darkTiers[t], darkTiers[t])
                 where l.C >= 0.12 && d.C >= 0.12
                 select (h.Key, h.Name, Tier: t, Light: l, Dark: d)).ToArray();
    int m = cands.Length;
    var pair = new double[m, m];
    for (int i = 0; i < m; i++)
        for (int j = 0; j < m; j++)
            pair[i, j] = Math.Min(DeltaE(cands[i].Light.Hex, cands[j].Light.Hex, null), DeltaE(cands[i].Dark.Hex, cands[j].Dark.Hex, null));

    double bestMin = 0;
    int[] bestPick = [];
    var pick = new List<int>();
    void Dfs(int from, double worst)
    {
        if (pick.Count == count)
        {
            if (worst > bestMin) { bestMin = worst; bestPick = [.. pick]; }
            return;
        }

        for (int i = from; i < m; i++)
        {
            if (pick.Any(p => cands[p].Key == cands[i].Key)) continue;
            double w = worst;
            foreach (int p in pick) w = Math.Min(w, pair[p, i]);
            if (w <= bestMin) continue;
            pick.Add(i);
            Dfs(i + 1, w);
            pick.RemoveAt(pick.Count - 1);
        }
    }

    Dfs(0, double.MaxValue);
    Console.WriteLine(string.Create(inv, $"худшая пара обычным зрением: {bestMin:0.0}"));
    foreach (int i in bestPick)
    {
        var c = cands[i];
        Console.WriteLine(string.Create(inv, $"{c.Name,-12} ступень {c.Tier}  {Describe(c.Light, "#FFFFFF")}   {Describe(c.Dark, "#1C2124")}"));
    }

    return 0;
}

// Перебор: все наборы из n тонов, худшая пара по обычному зрению в обеих темах
int n = args.Length > 1 ? int.Parse(args[1], inv) : 8;
string[] exclude = args.Length > 2 ? args[2].Split(',') : [];
string[] pool = [.. hues.Select(h => h.Key).Where(k => !exclude.Contains(k))];
var best = new List<(double Normal, double Cvd, string[] Set)>();
foreach (string[] set in Combinations(pool, n))
{
    double normal = double.MaxValue, cvd = double.MaxValue;
    for (int i = 0; i < set.Length; i++)
    {
        for (int j = i + 1; j < set.Length; j++)
        {
            foreach (var theme in new[] { light, dark })
            {
                string a = theme[set[i]].Hex, b = theme[set[j]].Hex;
                normal = Math.Min(normal, DeltaE(a, b, null));
                cvd = Math.Min(cvd, Math.Min(DeltaE(a, b, Deutan), DeltaE(a, b, Protan)));
            }
        }
    }

    best.Add((normal, cvd, set));
}

foreach (var (normal, cvd, set) in best.OrderByDescending(b => Math.Min(b.Normal / 15, 1) + Math.Min(b.Cvd / 8, 1)).ThenByDescending(b => b.Normal).Take(8))
{
    Console.WriteLine(string.Create(inv, $"обычное {normal,5:0.0}  дальтонизм {cvd,5:0.0}  {string.Join(", ", set)}"));
}

return 0;

string Describe((string Hex, double L, double C) c, string card)
{
    double white = Contrast(c.Hex, "#FFFFFF"), ink = Contrast(c.Hex, "#16191C"), bg = Contrast(c.Hex, card);
    return string.Create(inv, $"{c.Hex} L{c.L:0.00} C{c.C:0.000} к фону {bg:0.0} белый {white:0.0} тёмный {ink:0.0}");
}

// Самая насыщенная точка тона в полосе светлоты, ещё влезающая в sRGB
static (string Hex, double L, double C) Best(double hue, double lMin, double lMax)
{
    (string Hex, double L, double C) best = ("", 0, -1);
    for (double l = lMin; l <= lMax + 1e-9; l += 0.005)
    {
        double lo = 0, hi = 0.4;
        for (int k = 0; k < 30; k++)
        {
            double mid = (lo + hi) / 2;
            if (InGamut(l, mid, hue)) lo = mid; else hi = mid;
        }

        // Чуть внутрь границы: на самом краю sRGB цвет кричит и плохо переносится между экранами
        double c = Math.Min(lo * 0.92, 0.19);
        if (c > best.C) best = (ToHex(l, c, hue), l, c);
    }

    return best;
}

static bool InGamut(double l, double c, double h)
{
    var (r, g, b) = OklchToLinear(l, c, h);
    return r is >= -1e-6 and <= 1 + 1e-6 && g is >= -1e-6 and <= 1 + 1e-6 && b is >= -1e-6 and <= 1 + 1e-6;
}

static (double, double, double) OklchToLinear(double l, double c, double h)
{
    double a = c * Math.Cos(h * Math.PI / 180), bb = c * Math.Sin(h * Math.PI / 180);
    double l_ = l + 0.3963377774 * a + 0.2158037573 * bb, m_ = l - 0.1055613458 * a - 0.0638541728 * bb, s_ = l - 0.0894841775 * a - 1.2914855480 * bb;
    double L3 = l_ * l_ * l_, M3 = m_ * m_ * m_, S3 = s_ * s_ * s_;
    return (4.0767416621 * L3 - 3.3077115913 * M3 + 0.2309699292 * S3, -1.2684380046 * L3 + 2.6097574011 * M3 - 0.3413193965 * S3, -0.0041960863 * L3 - 0.7034186147 * M3 + 1.7076147010 * S3);
}

static string ToHex(double l, double c, double h)
{
    var (r, g, b) = OklchToLinear(l, c, h);
    static int Channel(double v) => (int)Math.Round(255 * Math.Clamp(v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055, 0, 1));
    return $"#{Channel(r):X2}{Channel(g):X2}{Channel(b):X2}";
}

static double[] Linear(string hex) => [.. new[] { 1, 3, 5 }.Select(i => Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0).Select(c => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4))];

static double[] Oklab(double[] v)
{
    double l = Math.Cbrt(0.4122214708 * v[0] + 0.5363325363 * v[1] + 0.0514459929 * v[2]);
    double m = Math.Cbrt(0.2119034982 * v[0] + 0.6806995451 * v[1] + 0.1073969566 * v[2]);
    double s = Math.Cbrt(0.0883024619 * v[0] + 0.2817188376 * v[1] + 0.6299787005 * v[2]);
    return [0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s, 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s, 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s];
}

static double[] Deutan(double[] v) => Apply([[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.011820, 0.042940, 0.968881]], v);
static double[] Protan(double[] v) => Apply([[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]], v);
static double[] Apply(double[][] m, double[] v) => [.. m.Select(r => Math.Clamp(r[0] * v[0] + r[1] * v[1] + r[2] * v[2], 0, 1))];

static double DeltaE(string a, string b, Func<double[], double[]>? sim)
{
    double[] x = Oklab(sim is null ? Linear(a) : sim(Linear(a))), y = Oklab(sim is null ? Linear(b) : sim(Linear(b)));
    return 100 * Math.Sqrt((x[0] - y[0]) * (x[0] - y[0]) + (x[1] - y[1]) * (x[1] - y[1]) + (x[2] - y[2]) * (x[2] - y[2]));
}

static double Contrast(string a, string b)
{
    static double Lum(string h) { double[] v = Linear(h); return 0.2126 * v[0] + 0.7152 * v[1] + 0.0722 * v[2]; }
    double x = Lum(a), y = Lum(b);
    return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
}

static IEnumerable<string[]> Combinations(string[] items, int k)
{
    if (k == 0) { yield return []; yield break; }
    for (int i = 0; i <= items.Length - k; i++)
        foreach (string[] rest in Combinations(items[(i + 1)..], k - 1))
            yield return [items[i], .. rest];
}
