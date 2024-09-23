import json
import os

INPUT_FOLDER = r'C:\Users\PerfectlyNormalBeast\Desktop\New folder (6)\adamsmasher-output\20240922 195159 attempt1'

def get_texttag_from_json(file_name):
        retVal = []

        with open(file_name) as file:
            data = json.load(file)
            for snippet in data["Snippets"]:
                text = snippet["Text"]
                retVal.append(text)

        return retVal

def get_sizes(snippets):
    total_size = 0
    sizes = []
    max_size = 0

    for snippet in snippets:
        size = len(snippet)
        total_size += size
        sizes.append(size)
        if size > max_size:
            max_size = size
    
    return total_size, sizes, max_size

def print_top_three(file_sizes, sort_key, title):
    # Sort the file_sizes list in descending order based on the provided sort key
    sorted_files = sorted(file_sizes, key=sort_key, reverse=True)

    # Print the top three entries
    print(title)
    for i in range(3):
        if i < len(sorted_files):
            file_name, file_size, max_size, text_sizes = sorted_files[i]
            key_value = sort_key((file_name, file_size, max_size, text_sizes))      # use the sort_key function to return an entry out of the tuple that is the same structure as what's in file_sizes list
            print(f"{i+1}. {file_name}: {key_value} bytes")

if __name__ == "__main__":
    file_sizes = []

    for file_name in os.listdir(INPUT_FOLDER):
        if file_name.endswith('.json'):
            snippets = get_texttag_from_json(os.path.join(INPUT_FOLDER, file_name))
            file_size, text_sizes, max_size = get_sizes(snippets)
            file_sizes.append((file_name, file_size, max_size, text_sizes))

    print_top_three(file_sizes, lambda x: x[1], "Top three files by total size:")
    print()
    print_top_three(file_sizes, lambda x: x[2], "Top three files by maximum snippet size:")
