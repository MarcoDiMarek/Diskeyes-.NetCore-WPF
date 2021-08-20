# Diskeyes
!(https://github.com/MarcoDiMarek/Diskeyes-.NetCore-WPF/blob/master/diskeyes%20git%20normal.png?raw=true)
Diskeyes is a tool to manage personal movie collections.
Optionally, Diskeyes should also facilitate exploring new movies based on what other people watch.
It provides a fast search with low demands on memory.

## Search
So far, the search operates as if each part of the query was joined with an OR operator. NOT operator can be used too, but needs to be stated explicitly.
For now, Diskeyes uses its own column-based database.
The system is intended to keep the data human-readable, which determines how data is structured, written and read.
Each column object manages its respective *.LineDB* files and *.CHANGES* files.
*.CHANGES* files are used to avoid rewriting of the *LineDB* files each time an entry is changed/added.
Changes are at some point merged in the background into a temporary file, which will upon success replace the original.
*Column* objects can be collected into a *Table* object.
A *Table* is a gateway between a query-like object containing a processed query, and *Column* objects called to search for specific fields of the query.
Search goes on asynchronously and matching entries are collected into batches that are passed to a Progress reporter method via a *Progress* object Task argument.
Batches are sent to a *SearchResults* object which aggregates the received matches into *SearchEntry* objects stored in a *ConcurrentDictionary*.
When the defined memory is exceeded, results are sorted by their score (calculated by the *SearchEntry* object) and the worst results are removed.
The sorted collection of partial results is then passed with the event which signals any subscribers that partial results are available.
If *Column* objects are used in a *Table*, partial results are handled by the event handler method inside the Table object.
With partial results received, the *Table* looks at the top results and sends the index of each entry to *Column* objects holding actual data (as opposed to those with just data indices).
Any new partial results are again passed to the data columns which continually report back the retrieved entry data.
At the *Task* level, the progress handler method passes retrieved batches to the same SearchResults.
When retrieval Tasks finish, partial results are updated with the retrieved data and an event is raised on the *Table* level.
This way, partial results can be processed by the UI or another wrapper of the Table object.

## Query
*Query* holds static references to Vocabulary objects.
*Query* can be passed query parsers and tokenizers for each possible search field. E.g., actors are tokenized by commas and a movie title by spaces.
*Query* separates a text entry into search fields and assigns indices to be looked-up.
A query example: **Raider actors(NOT Angelina Jolie)**

## UI - plans and mock-ups
No data processing on the UI thread.
UI reacts to events and stays as separate from the core logic as possible.
UI can call public methods of the database, but no workarounds should ever be made to allow direct access to *Table* or *Column* instances.

## Guidelines
The essential functionality should keep using a high level of abstraction, which can be modified/extended by other classes.
Embrace Generics and LINQ, but **never use dynamic type**.
Avoid Reflection and offload as much work as you can from the UI thread.
For documentation, use the format parsable by Visual Studio.
With a stable release of **.NET 6**, the project aims to abandon .NET Core and use .NET 6 for mobile as well.

