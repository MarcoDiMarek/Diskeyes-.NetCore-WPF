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
    class SearchEntry
    {
        public bool[] titleIndices;
        public bool[] descriptionIndices;
        public bool[] actors;
        public string title;
        public string description;
        public readonly int index;
        public int Score { get { return score; } }
        private int score;
        public SearchEntry(int index)
        {
            this.index = index;

            score = 0;
            description = null;
            title = null;
            // ENUMERABLES:
            actors = new bool[0];
            descriptionIndices = new bool[0];
            titleIndices = new bool[0];
        }
        private void SetScore()
        {
            score = titleIndices.Count(x => x) * 10
                    + descriptionIndices.Count(x => x)
                    + actors.Count(x => x) * 5;
        }
        public void Update(SearchCategory category, bool[] presence)
        {
            switch (category)
            {
                case SearchCategory.descriptionIndices:
                    {
                        descriptionIndices = presence;
                        break;
                    }
                case SearchCategory.titleIndices:
                    {
                        titleIndices = presence;
                        break;
                    }
                case SearchCategory.actorsIndices:
                    {
                        actors = presence;
                        break;
                    }
                default: throw new ArgumentException();
            }
            SetScore();
        }
        public void Update(SearchCategory category, string text)
        {
            switch (category)
            {
                case SearchCategory.title:
                    {
                        title = text;
                        break;
                    }
                case SearchCategory.description:
                    {
                        description = text;
                        break;
                    }
                default: throw new ArgumentException();
            }
            SetScore();
        }

    }
    struct SearchBatch
    {
        public readonly ReadOnlyCollection<(int, bool[])> BoolValues;
        public readonly int CategoryIdentifier;
        public SearchBatch(ReadOnlyCollection<(int, bool[])> boolValues, int categoryIdentifier = 0)
        {
            this.CategoryIdentifier = categoryIdentifier;
            BoolValues = boolValues;
        }
    }

    class Query
    {
        public readonly ReadOnlyDictionary<SearchCategory, string[]> QueryData;
        private static Vocabulary ActorsVocab = new Vocabulary("vocabulary_actors");
        private static Vocabulary GeneralVocab = new Vocabulary("vocabulary_general");
        public Query(string text, Dictionary<SearchCategory, Func<string, string[]>> tokenizers = null, Func<string, Dictionary<SearchCategory, string>> customParser = null)
        {
            var parser = customParser is null ? DefaultParser : customParser;
            if (tokenizers is null)
            {
                tokenizers = new Dictionary<SearchCategory, Func<string, string[]>>()
                {
                    { SearchCategory.actors, CommaTokenizer },
                    { SearchCategory.description, CommaTokenizer},
                    { SearchCategory.title, SpaceTokenizer },
                };
            }
            var data = new Dictionary<SearchCategory, string[]>();
            foreach (var (category, fieldText) in parser(text))
            {
                var tokenizer = tokenizers[category];
                data.Add(category, tokenizer(fieldText));
            }
            AssignIndices(ref data);
            QueryData = new ReadOnlyDictionary<SearchCategory, string[]>(data);
        }
        private static string[] SpaceTokenizer(string categoryText)
        {
            var tokens = categoryText.Split(" ").Select(x => x.Trim().ToLower());
            return tokens.ToArray();
        }
        private static string[] CommaTokenizer(string categoryText)
        {
            var tokens = categoryText.Split(",").Select(x => x.Trim().ToLower());
            return tokens.ToArray();
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

        private static void AssignIndices(ref Dictionary<SearchCategory, string[]> tokenized)
        {
            var tokenCategories = new SearchCategory[]
            {
                SearchCategory.title,
                SearchCategory.description,
                SearchCategory.actors
            };

            foreach (var tokenCategory in tokenCategories.Intersect(tokenized.Keys))
            {
                var (vocabulary, indexCategory) = TokenToIndexer(tokenCategory);
                var tokens = tokenized[tokenCategory];
                tokenized[indexCategory] = vocabulary.FindIndices(tokens).Select(x => x.ToString()).ToArray();
            }
        }
        private static (Vocabulary, SearchCategory) TokenToIndexer(SearchCategory tokenCategory)
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