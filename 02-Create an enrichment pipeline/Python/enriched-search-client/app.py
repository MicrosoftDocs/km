import os
from flask import Flask, request, render_template, redirect, url_for
from dotenv import load_dotenv

app = Flask(__name__)

# Azure Search constants
load_dotenv()
azsearch_url = os.getenv('AZ_SEARCH_ENDPOINT')
azsearch_key = os.getenv('AZ_SEARCH_KEY')

# Wrapper function for REST request to search index
def azsearch_query(index, params):
    # Imports and constants
    import http.client, urllib.request, urllib.parse, urllib.error, base64, json, urllib

    # Request headers.
    headers = {
        'Content-Type': 'application/json',
        'api-key': azsearch_key
    }
    
    try:
        # Execute the REST API call and get the response.
        conn = http.client.HTTPSConnection(azsearch_url)
        request_path = "/indexes/{0}/docs?{1}".format(index, params)
        conn.request("GET", request_path, None, headers)
        response = conn.getresponse()
        data = response.read().decode("UTF-8")
        result = json.loads(data)
        return result

    except Exception as ex:
        raise ex

# Home page route
@app.route("/")
def home():
    return render_template("default.html")

# Search results route
@app.route("/search", methods=['GET'])
def search():
    import urllib.parse, json

    try:

        # Get the search terms from the request form
        search_terms = request.args["search"]

        # Define the search parameters
        searchParams = urllib.parse.urlencode({
            'search':'{0}'.format(search_terms),
            'searchMode':'All',
            '$count':'true',
            'queryType':'simple',
            '$select':'*',
            'facet':'author',
            'highlight':'content-3,image_captions-3',
            'api-version':'2020-06-30-Preview'
        })

        # submit the query and get the results
        result = azsearch_query(index="margies-index-py", params=searchParams)
        hits = result['@odata.count']
        facets = result['@search.facets']['author']
        results = result["value"]
        return render_template("search.html", hitcount=hits, search_results=results, facets=facets, search_terms=search_terms)

    except Exception as error:
        return render_template("error.html", error_message=error)

# Filter route
@app.route("/filter", methods=['GET'])
def filter():
    import urllib.parse, json

    try:

        # Get the search terms in the query
        search_terms = request.args["search"]

        # Get the selected facet to filter
        if 'facet' in request.args:
            # filter based on the selected author facet
            facet_filter = "author eq '{0}'".format(request.args["facet"])
        else:
            # No facet specified, so show all docs with non-null authors
            facet_filter = "author ne null"

        # Order by search score (desc) (this is the default)
        sort_expression = 'search.score()'
        sort_field = 'relevance'

        # If a sort field is specified, modify the search expression accordingly
        if 'sort' in request.args:
            sort_field = request.args["sort"]
            if sort_field == 'file_name':
                sort_expression = 'name asc'
            elif sort_field == 'size':
                sort_expression = 'size desc'
            elif sort_field == 'date':
                sort_expression = 'last_modified desc'
            elif sort_field == 'sentiment':
                sort_expression = 'sentiment desc'


        # Define the search parameters
        searchParams = urllib.parse.urlencode({
            'search':'{0}'.format(search_terms),
            'searchMode':'All',
            '$count':'true',
            'queryType':'full',
            '$select':'*',
            'facet':'author',
            '$filter':facet_filter,
            '$orderby': sort_expression,
            'highlight':'content-3,image_captions-3',
            'api-version':'2020-06-30-Preview'
        })
        result = azsearch_query(index="margies-index-py", params=searchParams)
        hits = result['@odata.count']
        facets = result['@search.facets']['author']
        results = result["value"]
        return render_template("search.html", hitcount=hits, search_results=results, facets=facets, search_terms=search_terms, sort_field=sort_field)
    
    except Exception as error:
        return render_template("error.html", error_message=error)
