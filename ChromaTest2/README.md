I'm making a RAG tool in c#, which uses Microsoft.SemanticKernel as a wrapper to llm, sql

The problem is, the current implementation of semantic kernel doesn't support chroma writing to a db in process

This folder will use the python implementation of chroma to write to a local db (sqlite) and expose http endpoints

The c# app will then kick of this python process while it needs it

WARNING: don't have two instances of the c# app making two instances of this python process, which might corrupt the chroma db

[here](https://docs.trychroma.com/getting-started) is a link to the chroma api

# Temp Solution
Once semantic kernel supports local chroma db in process, there will be no need for this python implementation

# Future
I want this app to support rag dbs sort of like mods.  Each folder will be a self contained sql db and chroma db, representing whatever source code repo was parsed to populate it

This way there's not some single monolith db that has to be maintained over time and support periodic additions, cleanup

For that to work, the RAG builder app would point to a source code repo, make a copy of this chroma folder (and create a new .venv, do pip install), then fill up a sql db and chroma db

When finished, the output folder would be placed in some final folder, possibly next to other rag folders representing similar code.  That also makes it easy to share the built dbs

The search tool would then point to some root folder containing the rag subfolders