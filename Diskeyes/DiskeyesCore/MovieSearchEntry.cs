using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace DiskeyesCore
{
    class MovieSearchEntry : SearchEntry<SearchCategory>
    {
        public bool[] titleIndices;
        public bool[] descriptionIndices;
        public bool[] actors;
        public string title;
        public string description;

        public MovieSearchEntry()
        {
            score = 0;
            description = null;
            title = null;
            // ENUMERABLES:
            actors = new bool[0];
            descriptionIndices = new bool[0];
            titleIndices = new bool[0];
        }
        protected override void SetScore()
        {
            score = titleIndices.Count(x => x) * 10
                    + descriptionIndices.Count(x => x)
                    + actors.Count(x => x) * 5;
        }
        public override void Update(SearchCategory category, bool[] presence)
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
            recalculateScore = true;
        }
        public override void Update(SearchCategory category, string text)
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
            recalculateScore = true;
        }
    }
}
