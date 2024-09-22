# SourceCodeRAG
This is intended to be a tool to analyze a source code solution

# RAGSnippetBuilder
Point this at a folder that contains source code and it will break the files into snippets of text

Each snippet will be a single function, or enum, or top of a class

The snippets can then be stored in a sql database (see Models\CodeFile for properties)

Each snippet gets a unique ID (primary key of row in sql table).  That snippet text and ID are then handed to a rag db

### Ideas
It would also be nice to ask an llm for a summary of each class, maybe each function.  Those summaries would be stored in different tables of the databases

- table for raw code
- table for code summaries
- table for dump of discord channels

# And then...
Once the sql and rag dbs are populated, there should be llm agents that can answer questions

I'd like to make a chat style tool with multiple output tabs fed by different output agents

- agent that answers chat style
- agent that tries to make a wiki style response
- agent that always generates code
- another tab would be cluster plot of rag responses (mouse over the hits to see summary cards)