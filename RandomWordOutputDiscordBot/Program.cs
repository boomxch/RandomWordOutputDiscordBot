using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace RandomWordOutputDiscordBot
{
    // 参考 : http://kagasu.hatenablog.com/entry/2017/07/18/113335 様
    internal class Program
    {
        private const string BotName = "";

        private static void Main() => MainAsync().Wait();

        private static async Task MainAsync()
        {
            var client = new DiscordSocketClient();
            const string token = "";

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            client.MessageReceived += Client_MessageReceived;

            Console.ReadLine();
        }

        private static async Task Client_MessageReceived(SocketMessage arg)
        {
            Console.WriteLine(arg.Author.Username + " : " + arg.Content);

            if (!arg.Author.Username.Equals(BotName))
            {
                var rwgc = new RandomWordGenerateClass();

                if (Regex.IsMatch(arg.Content, rwgc.AllRandomRegex))
                {
                    var strOutput = rwgc.GetRandomWordOutput(true);
                    if (rwgc.CheckIsReactionEmpty(strOutput, out var str))
                        await arg.Channel.SendMessageAsync(str);

                    if (rwgc.SearchFileAttachment(strOutput, out var filePath))
                        await arg.Channel.SendFileAsync(filePath);
                }
                else if (Regex.IsMatch(arg.Content, rwgc.ArrangementRandomRegex))
                {
                    var strOutput = rwgc.GetRandomWordOutput();
                    if (rwgc.CheckIsReactionEmpty(strOutput, out var str))
                        await arg.Channel.SendMessageAsync(str);

                    if (rwgc.SearchFileAttachment(strOutput, out var filePath))
                        await arg.Channel.SendFileAsync(filePath);
                }
            }
        }
    }

    class RandomWordGenerateClass
    {
        public string OriginWord = "";
        public string AllRandomRegex = @"^$";
        public string ArrangementRandomRegex = @"^$";
        private readonly char[] _characters;
        private readonly Dictionary<char, int> _numberofCharacters = new Dictionary<char, int>();

        /*
         * Key items
         * {All}:全てのテキスト、ファイルに作用する　ただし生成された文字列と一致する連奏配列の項目がない場合のみ
         * {OriginalText}:OriginWordと同一の文字列にのみ、作用する
         *
         * Value items (_fileReactionsには作用しない)
         * {OriginalText}:この文字列をOriginWordに変換する
         * {Text}:この文字列の代わりに生成されたテキストを出力する
         * {Tex}:この文字列を生成されたテキストの一文字に変換し、これを含む一文を繰り返し生成し、つなげる
         * {SpaceFromMin}:この文字列を0個～(生成されたテキストの文字数-1)分のスペースに変換する
         * {SpaceFromMax}:この文字列を(生成されたテキストの文字数-1)分～0個のスペースに変換する
         * {SpaceFromRandom}:この文字列を0個～(生成されたテキストの文字数-1)分のスペースに、ランダムかつ重複しないよう変換する
         * {None}:この文字列が存在する場合、テキスト出力もしくはファイル出力をしない 確率を下げる項目
         */
        private readonly Dictionary<string, List<string>> _reactions = new Dictionary<string, List<string>>()
        {
            {
                "{OriginalText}",new List<string>()
                {
                    "＿人人人人人人人＿\n＞　{OriginalText}　＜\n￣Y^Y^Y^Y^Y^Y￣",
                }
            },
            {
                "{All}",new List<string>()
                {
                    "{Text}",
                    "{Tex}\n",
                    "{Tex}{SpaceFromMin}{SpaceFromMin}{Tex}\n",
                    "*{SpaceFromMin}{SpaceFromMin}{Tex}\n",
                    "{Tex}{SpaceFromMax}{SpaceFromMax}{Tex}\n",
                    "・{SpaceFromMax}{SpaceFromMax}{Tex}\n",
                    "{Tex}{SpaceFromRandom}{SpaceFromRandom}{Tex}\n",
                    "@{SpaceFromRandom}{SpaceFromRandom}{Tex}\n",
                    "|{Tex}|     |{Tex}|     |{Tex}|\n",
                }
            }
        };

        private readonly Dictionary<string, List<string>> _fileReactions = new Dictionary<string, List<string>>()
        {
            {
                "{OriginalText}",new List<string>()
                {
                    @"",
                }
            },
            {
                "{All}",new List<string>()
                {
                    @"{None}",
                }
            },
        };

        public RandomWordGenerateClass(string originWord = "", string allRandomRegex = "", string arrangementRandomRegex = "")
        {
            if (!originWord.Equals(string.Empty))
                OriginWord = originWord;

            if (!allRandomRegex.Equals(string.Empty))
                AllRandomRegex = allRandomRegex;

            if (!arrangementRandomRegex.Equals(string.Empty))
                ArrangementRandomRegex = arrangementRandomRegex;

            if (OriginWord.Equals(string.Empty))
            {
                Console.WriteLine("Error occured.\nOriginWord is empty.\nPress any key to stop the application.");
                Console.ReadKey();
                Environment.Exit(0);
            }

            var charas = new List<char>();

            foreach (var item in OriginWord)
            {
                if (!charas.Contains(item))
                {
                    charas.Add(item);
                    _numberofCharacters.Add(item, 0);
                }
                _numberofCharacters[item]++;
            }

            charas.Sort();
            _characters = charas.ToArray();
        }

        public string GetRandomWordOutput(bool isAllRandom = false)
        {
            var ans = string.Empty;

            if (isAllRandom)
            {
                var rnd = new Random();
                for (var i = 0; i < OriginWord.Length; i++)
                {
                    ans += _characters[rnd.Next(_characters.Length)];
                }
            }
            else
            {
                var tempCharas = _characters.Aggregate(string.Empty, (current, t) => current + new string(t, _numberofCharacters[t]));

                ans = new string(tempCharas.OrderBy(i => Guid.NewGuid()).ToArray());
            }

            return ans;
        }

        public bool SearchFileAttachment(string str, out string filePath)
        {
            if (ReactionDictionaryValueExists(_fileReactions, str))
            {
                var path = _fileReactions[str].OrderBy(i => Guid.NewGuid()).FirstOrDefault();

                if (path?.IndexOf("{None}", StringComparison.Ordinal) == -1 && File.Exists(path))
                {
                    filePath = path;
                    return true;
                }
            }
            else if (str == OriginWord && ReactionDictionaryValueExists(_fileReactions, "{OriginalText}"))
            {
                var path = _fileReactions["{OriginalText}"].OrderBy(i => Guid.NewGuid()).FirstOrDefault();
                if (path?.IndexOf("{None}", StringComparison.Ordinal) == -1 && File.Exists(path))
                {
                    filePath = path;
                    return true;
                }
            }
            else if (ReactionDictionaryValueExists(_fileReactions, "{All}"))
            {
                var path = _fileReactions["{All}"].OrderBy(i => Guid.NewGuid()).FirstOrDefault();
                if (path?.IndexOf("{None}", StringComparison.Ordinal) == -1 && File.Exists(path))
                {
                    filePath = path;
                    return true;
                }
            }

            filePath = string.Empty;
            return false;
        }

        public bool CheckIsReactionEmpty(string str, out string reaction)
        {
            reaction = GetReactionOutput(str);

            return reaction != string.Empty;
        }

        private string GetReactionOutput(string str)
        {
            if (ReactionDictionaryValueExists(_reactions, str))
            {
                var text = _reactions[str].OrderBy(i => Guid.NewGuid()).FirstOrDefault();
                if (text?.IndexOf("{None}", StringComparison.Ordinal) == -1)
                    return GetReaction(text, str);
            }
            else if (str == OriginWord && ReactionDictionaryValueExists(_fileReactions, "{OriginalText}"))
            {
                var text = _reactions["{OriginalText}"].OrderBy(i => Guid.NewGuid()).FirstOrDefault();
                if (text?.IndexOf("{None}", StringComparison.Ordinal) == -1)
                    return GetReaction(text, str);
            }
            else if (ReactionDictionaryValueExists(_reactions, "{All}"))
            {
                var text = _reactions["{All}"].OrderBy(i => Guid.NewGuid()).FirstOrDefault();

                if (text?.IndexOf("{None}", StringComparison.Ordinal) == -1)
                    return GetReaction(text, str);
            }

            return string.Empty;
        }
        private string GetReaction(string valueString, string text)
        {
            /* Value items
            * {OriginalText}:この文字列をOriginWordに変換する
            * {Text}:この文字列の代わりに生成されたテキストを出力する
            * {Tex}:この文字列を生成されたテキストの一文字に変換し、これを含む一文を
            * {SpaceFromMin}:この文字列を0個～(生成されたテキストの文字数-1)分のスペースに変換する
            * {SpaceFromMax}:この文字列を(生成されたテキストの文字数-1)分～0個のスペースに変換する
            * {SpaceFromRandom}:この文字列を0個～(生成されたテキストの文字数-1)分のスペースに、ランダムかつ重複しないよう変換する
            * {None}:この文字列が存在する場合、テキスト出力もしくはファイル出力をしない
            */

            valueString = valueString.Replace("{OriginalText}", OriginWord);
            valueString = valueString.Replace("{Text}", text);

            if (!Regex.IsMatch(valueString, "({Tex})|({SpaceFromMin})|({SpaceFromMax})|{SpaceFromRandom}"))
                return valueString;

            var ans = string.Empty;
            var length = text.Length;
            var spaceNums = Enumerable.Range(0, length).ToList();
            var spaceNumsRandom = Enumerable.Range(0, length).OrderBy(i => Guid.NewGuid()).ToList();
            foreach (var character in text)
            {
                var temp = valueString.Replace("{Tex}", character.ToString());

                temp = temp.Replace("{SpaceFromMin}", new string(' ', spaceNums.First()));
                temp = temp.Replace("{SpaceFromMax}", new string(' ', length - spaceNums.First() - 1));
                spaceNums.RemoveAt(0);

                temp = temp.Replace("{SpaceFromRandom}", new string(' ', spaceNumsRandom.First()));
                spaceNumsRandom.RemoveAt(0);

                ans += temp;
            }

            return ans;
        }

        private static bool ReactionDictionaryValueExists(IReadOnlyDictionary<string, List<string>> dic, string val)
        {
            return dic.ContainsKey(val) && dic[val].Count > 0;
        }
    }
}
