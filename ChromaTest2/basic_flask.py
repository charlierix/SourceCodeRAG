from flask import Flask, request, jsonify, url_for

app = Flask(__name__)

@app.route('/add', methods=['POST'])
def add():
    print('add called')
    return "Success", 200

if __name__ == '__main__':
    print('before run')
    app.run()
    print('after run')