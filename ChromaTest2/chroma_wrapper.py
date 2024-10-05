import chromadb
import re
from flask import Flask, request, jsonify

app = Flask(__name__)

chroma_client = chromadb.PersistentClient(path='./chroma')
MAX_BATCH_SIZE = max(1, chroma_client.get_max_batch_size() - 1)      # collection.add will throw exception if too many entries are passed in at once (subtracting one for a bit of margin)
collections = {}  # Dictionary to store collections by name

# Adds a set of IDs and Vectors to a collection (creates the collection if this is the first ever add to it)
# Request Object:
# {
# 	"collection": "name",
# 	"ids": ["1", "2"],
# 	"vectors": [
# 		[1,2,3,4],
# 		[5,6,7,8]
# 	]
# }
@app.route('/add', methods=['POST'])
def add():
    if request.is_json:
        data = request.get_json()
        is_valid, err_msg = validate_add(data)
        if is_valid:
            try:
                collection = ensure_collection_created(data['collection'])
                add_entries(collection, data['ids'], data['vectors'])

                return "Success"
            except Exception as ex:
                return str(ex), 500
        else:
            return err_msg, 400
    else:
        return "Request does not contain valid JSON", 400

# Searches for nearby vectors
# Request Object:
# {
# 	"collection": "name",
# 	"vector": [1,2,3,4],
# 	"return_count": 12
# }
#
# Response Object:
# {
# 	"ids": ["1", "2" ... ],
# 	"scores": [0.5, 0.2 ... ],
# }
@app.route('/query', methods=['POST'])
def query():
    if request.is_json:
        data = request.get_json()
        is_valid, err_msg = validate_query(data)
        if is_valid:
            try:
                collection = ensure_collection_created(data['collection'])
                results = do_query(collection, data['vector'], data['return_count'])

                return jsonify(results)  # Return results as JSON response
            except Exception as ex:
                return str(ex), 500
        else:
            return err_msg, 400
    else:
        return "Request does not contain valid JSON", 400

def validate_add(data):
    # Here are the full requirements for collection name, but just let it throw an excpection instead of making a monster regex
    # In some ways, \w is too strict, but it's good enough
    #
    # Chroma uses collection names in the url, so there are a few restrictions on naming them:
    #   The length of the name must be between 3 and 63 characters.
    #   The name must start and end with a lowercase letter or a digit, and it can contain dots, dashes, and underscores in between.
    #   The name must not contain two consecutive dots.
    #   The name must not be a valid IP address.

    if 'collection' not in data or type(data['collection']) != str or not re.match('^\w+$', data['collection']):
        return False, 'Collection name is missing or invalid'

    if 'ids' not in data or 'vectors' not in data:
        return False, 'ids and vectors are required'

    ids = data['ids']
    vectors = data['vectors']

    if len(ids) != len(vectors):
        return False, 'IDs and vectors must have the same length'

    for i in range(len(ids)):
        if type(ids[i]) != str or not re.match('^\w+$', ids[i]):
            return False, f'Invalid ID at index {i}: {ids[i]} (IDs must be strings that match the pattern \\w+)'
        if type(vectors[i]) != list or any(type(x) not in [int, float] for x in vectors[i]):
            return False, f'Invalid vector at index {i}: {vectors[i]} (Vectors must be lists of numbers)'

    return True, ''

def validate_query(data):
    if 'collection' not in data or type(data['collection']) != str or not re.match('^\w+$', data['collection']):
        return False, 'Collection name is missing or invalid'

    if 'vector' not in data:
        return False, 'vector is required'

    if type(data['vector']) != list or any(type(x) not in [int, float] for x in data['vector']):
        return False, f'Invalid vector: {data['vector']} (Vectors must be lists of numbers)'

    if 'return_count' not in data or type(data['return_count']) != int:
        return False, 'return_count must be integer'

    return True, ''

# Gets or creates the collection by name in the client, stores a reference to it in collections dict
def ensure_collection_created(name):
    if name not in collections:
        try:
            collection = chroma_client.get_collection(name)
        except:
            collection = chroma_client.create_collection(name, metadata={'hnsw:space': 'cosine'})       # 'l2' (len squared), 'ip' (inner product), 'cosine' (angle between vectors) -- doing some reading, cosine seems best for high dimensions, next best is distance

        collections[name] = collection

    return collections[name]

# Iterate the snippets, build a list no larger than threshold.  Add those, then keep looping until finished
# if one of the entries is larger than the threshold, figure out how to split into parts
def add_entries(collection, ids, vectors):
    set_count = 0
    set_ids = []
    set_vectors = []

    for i in range(len(ids)):
        if set_count + 1 > MAX_BATCH_SIZE:
            collection.add(ids=set_ids, embeddings=set_vectors)
            set_ids.clear()
            set_vectors.clear()
            set_count = 0

        else:
            set_ids.append(ids[i])
            set_vectors.append(vectors[i])
            set_count += 1

    if set_count > 0:
        collection.add(ids=set_ids, embeddings=set_vectors)

def do_query(collection, vector, return_count):
    # embeddings says OneOrMany, hopefully passing a single vector is enough
    response = collection.query(query_embeddings=vector, n_results=return_count, include=['distances'])     # include also supports 'metadatas' and 'documents', but those aren't stored (ids are always included)

    # TODO: set a breakpoint and see how response is structured
    return None