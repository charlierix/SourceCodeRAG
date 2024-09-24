import chromadb
import json
import os
import torch
import uuid

from chromadb.utils import embedding_functions

#INPUT_FOLDER = r'C:\Users\PerfectlyNormalBeast\Desktop\New folder (6)\adamsmasher-output\20240922 195159 attempt1'
INPUT_FOLDER = r'C:\Users\PerfectlyNormalBeast\Desktop\New folder (6)\adamsmasher-output\largest single file'
COLLECTION_NAME = 'test1'
USE_GPU = False
MAX_ADD_LEN = 1000000000

collection = None

# Read the folder of json files
# For each of the json files, add each snippet into the chroma db
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

# For this tester 1, just focus on the Text value
def get_texttag_from_json(file_name):
        retVal = []

        with open(file_name) as file:
            data = json.load(file)
            for snippet in data["Snippets"]:
                text = snippet["Text"]
                retVal.append(text)

        return retVal

# Iterate the snippets, build a list no larger than threshold.  Add those, then keep looping until finished
# if one of the entries is larger than the threshold, figure out how to split into parts
def insert_snippets(snippets):
    set_total = 0
    set = []

    for snippet in snippets:
        snip_len = len(snippet)

        if snip_len > MAX_ADD_LEN:
            print('TODO: individual snippet is too large')
            print(snippet)

        elif set_total + snip_len > MAX_ADD_LEN:
            insert_snippets_do_it(set)
            set.clear()
            set_total = 0

        else:
            set.append(snippet)
            set_total += snip_len

    if len(set) > 0:
        insert_snippets_do_it(set)

def insert_snippets_do_it(snippets):
    ids = generate_ids(len(snippets))

    # One of the files had a really big function.  Is it an individual snippet that's too large, or the whole list?
    # ValueError: Batch size 18436 exceeds maximum batch size 5461

    # single large function worked, so make an alternate function that batches the calls to collection.add based
    # on sum of size of items in list

    try:
        collection.add(documents=snippets, ids=ids)
    except Exception as ex:
        print('Error adding collection.  num snippets: ' + str(len(snippets)) + ', len snippets: ' + str(get_sum_len(snippets)))
        print(ex)

def get_sum_len(strings):
    retVal = 0
    for s in strings:
        retVal += len(s)
    return retVal

def generate_ids(count):
    ids = []
    for _ in range(count):
        ids.append(str(uuid.uuid4()))       # a real project would insert these snippets into a sql table and the id would be a primary key

    return ids

if __name__ == "__main__":
    #chroma_client = chromadb.Client()      # not sure when to use this, since the db doesn't seem to save between runs
    chroma_client = chromadb.PersistentClient(path='./chroma')     # using persistant so it saves db to chroma folder

    # Delete the collection in case this script is run multiple times
    try:
        chroma_client.delete_collection(COLLECTION_NAME)
    except:
        print("collection didn't exist")        # it keeps coming here, I'm guessing client or collection needs to be persistant

    emb_func = embedding_functions.DefaultEmbeddingFunction()
    if USE_GPU:
        # NOTE: this requires torch for cuda support, which requires nvidia cuda toolkit
        # https://www.educative.io/answers/how-to-resolve-torch-not-compiled-with-cuda-enabled
        # print("Torch version:", torch.__version__)
        # print("Is CUDA enabled?", torch.cuda.is_available())
        emb_func = embedding_functions.SentenceTransformerEmbeddingFunction(model_name=emb_func.MODEL_NAME, device='cuda', normalize_embeddings=True)     # model_name='all-MiniLM-L6-v2'  -- NOTE: needs pip install sentence_transformers
    collection = chroma_client.create_collection(COLLECTION_NAME)

    process_folder(INPUT_FOLDER)

    print(str(collection.count()) + ' snippets added')