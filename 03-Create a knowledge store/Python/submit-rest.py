import argparse

# Wrapper function for REST requests to Azure Cognitive Search service
def azsearch_rest(request_type="GET", function_name="servicestats", azsearch_url=None, azsearch_key=None, body=None):
    # Imports and constants
    import http.client, urllib.request, urllib.parse, urllib.error, base64, json, urllib

    # Request headers.
    headers = {
        'Content-Type': 'application/json',
        'api-key': azsearch_key
    }

    # Request parameters (specify the version of the API to use)
    params = urllib.parse.urlencode({
        'api-version':'2020-06-30-Preview'
    })
    
    try:
        # Execute the REST API call and get the response.
        conn = http.client.HTTPSConnection(azsearch_url)
        request_path = "/{0}?{1}".format(function_name, params)
        conn.request(request_type, request_path, body, headers)
        response = conn.getresponse()
        data = response.read().decode("UTF-8")
        result = None
        if len(data) > 0:
            result = json.loads(data)
        return result

    except Exception as ex:
        raise ex

# Main function
def main(request_type, function_name, json_file):
    import json, os
    from dotenv import load_dotenv

    # Get service connection details
    load_dotenv()
    az_search_endpoint = os.getenv('AZ_SEARCH_ENDPOINT')
    az_search_key = os.getenv('AZ_SEARCH_KEY')

 
    # Load request body JSON
    body = None
    if json_file != 'null':
        with open(json_file, 'r') as json_body:
            data = json.load(json_body)
            # If creating a datasource, update with the connection string for the blob store
            if json_file == 'data_source.json':
                az_blob_conn = os.getenv('AZ_BLOB_CONNECTION_STRING')
                data["credentials"]["connectionString"] = az_blob_conn
            # If creating a skillset, update with the cognitive services account key
            # and the storage connection string for the knowledge store
            elif json_file == 'skillset.json':
                cog_key = os.getenv('COG_SERVICE_KEY')
                data["cognitiveServices"]['key'] = cog_key
                az_blob_conn = os.getenv('AZ_BLOB_CONNECTION_STRING')
                data["knowledgeStore"]["storageConnectionString"] = az_blob_conn
        body = json.dumps(data)

    # Submit request
    result = azsearch_rest(request_type, function_name, az_search_endpoint, az_search_key, body)

    # Display results
    if result != None:
        print(json.dumps(result, sort_keys=True, indent=2))

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('request_type', help='HTTP request type')
    parser.add_argument('function_name', help='Azure search REST function')
    parser.add_argument('json_file', help='JSON request')
    args = parser.parse_args()
    main(args.request_type, args.function_name, args.json_file)