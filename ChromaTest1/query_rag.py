import chromadb

from chromadb.utils import embedding_functions

COLLECTION_NAME = 'test1'
USE_GPU = False

def report_results(results):
    ids = results['ids']
    dist = results['distances']
    docs = results['documents']

    if len(ids) == 0:
        print('--- NO RESULTS ---')
        return

    elif len(ids) > 1:
        print('--- UNEXPECTED RESULTS ---')

    for i in range(len(ids[0])):
        print('--------------------------')
        print('dist:\t' + str(dist[0][i]))
        print('id:\t' + ids[0][i])
        print(docs[0][i])
        print()
        print()
        print()

if __name__ == "__main__":
    chroma_client = chromadb.PersistentClient(path='./chroma')     # using persistant so it saves db to chroma folder

    emb_func = embedding_functions.DefaultEmbeddingFunction()
    if USE_GPU:
        # NOTE: this requires torch for cuda support, which requires nvidia cuda toolkit
        # https://www.educative.io/answers/how-to-resolve-torch-not-compiled-with-cuda-enabled
        # print("Torch version:", torch.__version__)
        # print("Is CUDA enabled?", torch.cuda.is_available())
        emb_func = embedding_functions.SentenceTransformerEmbeddingFunction(model_name=emb_func.MODEL_NAME, device='cuda', normalize_embeddings=True)     # model_name='all-MiniLM-L6-v2'  -- NOTE: needs pip install sentence_transformers
    collection = chroma_client.get_collection(COLLECTION_NAME)




    
    # The results of this are pretty much garbage.  Directly storing code in rag doesn't seem to give good results

    # Some approaches that may help get better results
    #   Have an llm describe each function snippet
    #   Have an llm interpret the function snippets into possible questions that might get asked about those functions
    #   Have an llm read this question and dream up a few ways to code it, then query based on the generated code




    question = ["How to get the player to ascend a ladder?"]

    # https://docs.trychroma.com/reference/py-collection#query
    results = collection.query(query_texts=question, n_results=12)
    report_results(results)