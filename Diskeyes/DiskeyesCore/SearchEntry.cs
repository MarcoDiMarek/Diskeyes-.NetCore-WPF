using System;
using System.Collections.Generic;
using System.Text;

namespace DiskeyesCore
{
    interface ISearchEntry<T>
    {
        // Interface is needed to allow for instantiation of a generic type of search entry, 
        // as abstract classes cannot be instantiated
        public void Update(T category, bool[] presence);
        public void Update(T category, string value);
        public int GetScore();
    }
    public abstract class SearchEntry<T> : ISearchEntry<T>
    {
        public int Score
        {
            get
            {
                if (recalculateScore)
                {
                    SetScore(); recalculateScore = false;
                }
                return score;
            }
        }
        public int GetScore() { return Score; }
        protected int score;
        protected bool recalculateScore;

        public abstract void Update(T category, bool[] presence);
        public abstract void Update(T category, string value);
        protected abstract void SetScore();
    }
}
