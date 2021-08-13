using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace DiskeyesCore
{
    class Vocabulary
    {
        private static Func<string, string> lambdaDummy = x => x;
        protected Dictionary<string, int> tokens;
        private Column<string> vocabDB;
        public Vocabulary(string name)
        {
            vocabDB = new Column<string>(name, Encoding.UTF8, lambdaDummy, lambdaDummy, 1);
            Task.WaitAll(vocabDB.Initialize());
            tokens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            tokens.EnsureCapacity(vocabDB.EntriesCount);
            foreach (var batch in vocabDB.Access())
            {
                foreach (var (index, token) in batch)
                {
                    if (tokens.ContainsKey(token))
                    {
                        vocabDB.Remove(index);
                    }
                    else
                    {
                        tokens[token] = index;
                    }
                }
            }
        }
        /// <summary>
        /// Adds the given token (term) to the vocabulary if it is not there yet.
        /// </summary>
        /// <param name="token">Token / term to be added.</param>
        /// <returns>False if term was already there.</returns>
        public bool AddUnique(string token)
        {
            if (tokens.ContainsKey(token) == false)
            {
                int id = vocabDB.Add(token);
                tokens[token] = id;
                return true;
            }
            else return false;
        }
        /// <summary>
        /// Attempts to remove an existing token.
        /// </summary>
        /// <param name="token">Token (term) to remove</param>
        /// <returns>True when token has been removed.</returns>
        public bool Remove(string token)
        {
            bool found = tokens.Remove(token);
            if (found) vocabDB.Remove(tokens[token]);
            return found;
        }
        /// <summary>
        /// Attempt to find the term (token) corresponding to a given index.
        /// Throws InvalidOperation exception if not found.
        /// </summary>
        /// <param name="index">Index of the token.</param>
        /// <returns>Token</returns>
        public string Token(int index) { return tokens.First(x => x.Value == index).Key; }

        public Dictionary<string, int> FindIndices(IEnumerable<string> words)
        {
            var matches = new Dictionary<string, int>();
            foreach (string word in words)
            {
                int index;
                if (tokens.TryGetValue(word, out index))
                {
                    matches.Add(word, index);
                }

            }
            return matches;
        }
        /// <summary>
        /// Returns index of the searched token. 
        /// Throws KeyNotFound exception if not found.
        /// </summary>
        /// <param name="token">Term/token searched for</param>
        /// <returns>Index of the token.</returns>
        public int Index(string token) { return tokens[token]; }
    }
}
