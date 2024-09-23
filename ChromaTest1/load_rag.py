import chromadb
import json
import os
import torch
import uuid

from chromadb.utils import embedding_functions

#INPUT_FOLDER = r'C:\Users\PerfectlyNormalBeast\Desktop\New folder (6)\adamsmasher-output\20240922 195159 attempt1'
INPUT_FOLDER = r'C:\Users\PerfectlyNormalBeast\Desktop\New folder (6)\adamsmasher-output\largest single file'
COLLECTION_NAME = 'test1'
USE_GPU = True

collection = None

def process_folder(folder_name):
    file_counter = 0
    snippet_counter = 0

    for file_name in os.listdir(folder_name):
        if file_name.endswith('.json'):
            snippets = get_texttag_from_json(os.path.join(folder_name, file_name))
            insert_snippets(snippets)

            # make a crude progress bar
            file_counter += 1
            snippet_counter += len(snippets)
            if file_counter % 100 == 0:
                print(str(file_counter) + ' files | ' + str(snippet_counter) + ' snippets')

def get_texttag_from_json(file_name):
        retVal = []

        with open(file_name) as file:
            data = json.load(file)
            for snippet in data["Snippets"]:
                text = snippet["Text"]
                retVal.append(text)

        return retVal

def insert_snippets(snippets):
    ids = generate_ids(len(snippets))




    # One of the files had a really big function.  Is it an individual snippet that's too large, or the whole list?
    # ValueError: Batch size 18436 exceeds maximum batch size 5461


    # single large function worked, so make an alternate function that batches the calls to collection.add based
    # on sum of size of items in list
    collection.add(documents=snippets, ids=ids)




def generate_ids(count):
    ids = []
    for _ in range(count):
        ids.append(str(uuid.uuid4()))       # a real project would insert these snippets into a sql table and the id would be a primary key

    return ids

if __name__ == "__main__":

    print("Torch version:", torch.__version__)
    print("Is CUDA enabled?", torch.cuda.is_available())


    #chroma_client = chromadb.Client()      # not sure when to use this, since the db doesn't seem to save between runs
    chroma_client = chromadb.PersistentClient(path='./chroma')     # using persistant so it saves db to chroma folder

    # Delete the collection in case this script is run multiple times
    try:
        chroma_client.delete_collection(COLLECTION_NAME)
    except:
        print("collection didn't exist")        # it keeps coming here, I'm guessing client or collection needs to be persistant

    emb_func = embedding_functions.DefaultEmbeddingFunction()
    if USE_GPU:
        emb_func = embedding_functions.SentenceTransformerEmbeddingFunction(model_name=emb_func.MODEL_NAME, device='cuda', normalize_embeddings=True)     # model_name='all-MiniLM-L6-v2'  -- NOTE: needs pip install sentence_transformers
        collection = chroma_client.create_collection(COLLECTION_NAME, embedding_function=emb_func)
    else:
        collection = chroma_client.create_collection(COLLECTION_NAME)

    process_folder(INPUT_FOLDER)

    print(str(collection.count()) + ' snippets added')