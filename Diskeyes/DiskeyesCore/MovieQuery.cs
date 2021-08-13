using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace DiskeyesCore
{
    enum SearchCategory : ushort
    {
        title,
        titleIndices,
        description,
        descriptionIndices,
        actors,
        actorsIndices,
        rating,
    }

    struct SearchBatch
    {
        public readonly ReadOnlyCollection<(int, bool[])> BoolValues;
        public readonly int CategoryIdentifier;
        public SearchBatch(ReadOnlyCollection<(int, bool[])> boolValues, int categoryIdentifier = 0)
        {
            CategoryIdentifier = categoryIdentifier;
            BoolValues = boolValues;
        }
    }
    struct SearchBatch<T>
    {
        public readonly ReadOnlyCollection<(int, T)> Values;
        public readonly int CategoryIdentifier;
        public SearchBatch(ReadOnlyCollection<(int, T)> values, int categoryIdentifier = 0)
        {
            CategoryIdentifier = categoryIdentifier;
            Values = values;
        }
    }

    struct QueryEntry
    {
        public string[] values;
        public bool[] desiredPresence;
        public IEnumerable<(string, bool)> Pairs()
        {
            return values.Zip(desiredPresence);
        }
    }

    interface IQuery<T>
    {
        public ReadOnlyDictionary<T, QueryEntry> GetQueryData();
    }

    /// <summary>
    /// Parses a user input string into search categories and indices.
    /// </summary>
    class MovieQuery : IQuery<SearchCategory>
    {
        public readonly ReadOnlyDictionary<SearchCategory, QueryEntry> QueryData;
        private static Vocabulary ActorsVocab = new Vocabulary("vocabulary_actors");
        private static Vocabulary GeneralVocab = new Vocabulary("vocabulary_general");
        public MovieQuery(string text, Dictionary<SearchCategory, Func<string, (string[], bool[])>> tokenizers = null, Func<string, Dictionary<SearchCategory, string>> customParser = null)
        {
            var parser = customParser is null ? DefaultParser : customParser;
            if (tokenizers is null)
            {
                tokenizers = new Dictionary<SearchCategory, Func<string, (string[], bool[])>>()
                {
                    { SearchCategory.actors, CommaTokenizer },
                    { SearchCategory.description, CommaTokenizer},
                    { SearchCategory.title, SpaceTokenizer },
                };
            }
            var data = new Dictionary<SearchCategory, QueryEntry>();
            foreach (var (category, fieldText) in parser(text))
            {
                var tokenizer = tokenizers[category];
                var (tokens, presence) = tokenizer(fieldText);
                QueryEntry entry = new QueryEntry()
                {
                    values = tokens,
                    desiredPresence = presence
                };
                data.Add(category, entry);
            }
            AssignIndices(ref data);
            QueryData = new ReadOnlyDictionary<SearchCategory, QueryEntry>(data);
        }
        public ReadOnlyDictionary<SearchCategory, QueryEntry> GetQueryData()
        {
            return QueryData;
        }
        private static (string[], bool[]) SpaceTokenizer(string categoryText)
        {
            const string pattern = "(NOT )?\\w+";
            return RegexTokenizer(categoryText, pattern);
        }
        private static (string[], bool[]) CommaTokenizer(string categoryText)
        {
            const string pattern = "(NOT )?(\\w+ ?)+";
            return RegexTokenizer(categoryText, pattern);
        }
        private static (string[], bool[]) RegexTokenizer(string categoryText, string pattern)
        {
            var matches = from match in Regex.Matches(categoryText, pattern, RegexOptions.Compiled)
                          where match.Success
                          select match.Value;
            List<string> tokens = matches.ToList();
            List<bool> desired = Enumerable.Repeat(true, tokens.Count).ToList();
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.StartsWith("NOT "))
                {
                    token = token.Substring(4).Trim().ToLower();
                    if (token.Length == 0)
                    {
                        tokens.RemoveAt(i);
                        desired.RemoveAt(i);
                    }
                    else
                    {
                        tokens[i] = token;
                        desired[i] = false;
                    }
                }
            }
            return (tokens.ToArray(), desired.ToArray());
        }

        private static Dictionary<SearchCategory, string> DefaultParser(string queryText)
        {
            queryText = queryText.Trim();
            const string paranthesis = "([.]*[^)]*)";
            var patterns = new Dictionary<SearchCategory, string>(3)
            {
                { SearchCategory.actors, "actors" + paranthesis },
                { SearchCategory.description, "desc(ription)?" + paranthesis  },
                { SearchCategory.rating, "rating" + paranthesis }
            };

            var output = new Dictionary<SearchCategory, string>();
            foreach (var (category, pattern) in patterns)
            {
                var match = Regex.Match(queryText, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string innerText = match.Value.Split("(")[1];
                    output[category] = innerText;
                    queryText = queryText.Replace(match.Value + ")", "");
                }
            }
            queryText = queryText.Trim();
            if (queryText.Length > 0)
            {
                // take the rest of the text as the title of the searched movie
                // so that the user does not have to wrap the title of the movie in parantheses
                output[SearchCategory.title] = queryText;
            }
            return output;
        }

        private static void AssignIndices(ref Dictionary<SearchCategory, QueryEntry> tokenizedEntries)
        {
            var indexedCategories = new SearchCategory[]
            {
                SearchCategory.title,
                SearchCategory.actors,
                SearchCategory.description
            };
            // QueryEntry is just a struct, so it is always value-copied.
            // Changes per collection member do not propagate to collection unless reassigned.
            foreach (var tokenCategory in indexedCategories.Intersect(tokenizedEntries.Keys))
            {
                var (vocabulary, indexCategory) = GetIndexer(tokenCategory);
                var entry = tokenizedEntries[tokenCategory];
                var found = vocabulary.FindIndices(entry.values);
                var matches = entry.Pairs()
                              .Where(x => found.ContainsKey(x.Item1))
                              .ToDictionary(x => x.Item1, x => x.Item2);

                entry.values = matches.Keys
                               .Select(x => found[x].ToString())
                               .ToArray();
                entry.desiredPresence = matches.Values.ToArray();
                tokenizedEntries[indexCategory] = entry;
            }
        }
        private static (Vocabulary, SearchCategory) GetIndexer(SearchCategory tokenCategory)
        {
            // Switch is internalized as a dictionary CONSTANT, therefore more efficient
            switch (tokenCategory)
            {
                case SearchCategory.title:
                    return (GeneralVocab, SearchCategory.titleIndices);
                case SearchCategory.description:
                    return (GeneralVocab, SearchCategory.descriptionIndices);
                case SearchCategory.actors:
                    return (ActorsVocab, SearchCategory.actorsIndices);
                default:
                    throw new ArgumentException();
            }
        }

    }
}