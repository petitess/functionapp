#requirements.txt
#azure-functions
#azure-mgmt-resource
#azure-identity

import json
import azure.functions as func
from azure.identity import DefaultAzureCredential
import logging
import os
import requests
import datetime

app = func.FunctionApp()

@app.route(route="http_trigger", auth_level=func.AuthLevel.ANONYMOUS)
def http_trigger(req: func.HttpRequest) -> func.HttpResponse:
    logging.info("1. Python HTTP trigger function processed a request.")

    now = datetime.datetime.now().strftime("%a, %d %b %Y %H:%M:%S GMT")
    sub_id = os.getenv("SUB_ID")
    st_name = os.getenv("ST_NAME")
    st_rg_name = os.environ.get("ST_RG_NAME", f"rg-{st_name}")
    cont_name = os.getenv("ST_CONT_NAME")
    credential = DefaultAzureCredential()
    logging.info("2. Using DefaultAzureCredential for authentication.")

    scopes = ["https://management.azure.com/.default"]
    token = credential.get_token(*scopes)
    url = f"https://management.azure.com/subscriptions/{sub_id}/resourceGroups/{st_rg_name}/providers/Microsoft.Storage/storageAccounts/{st_name}/blobServices/default/containers/{cont_name}?api-version=2025-01-01"
    headers = {
        "Authorization": f"Bearer {token.token}",
        "Content-Type": "application/json",
    }
    try:
        response_get = requests.get(url, headers=headers)
    except Exception as e:
        logging.error(f"Error during GET request to Azure Management API: {e}")
        return func.HttpResponse(
            f"Error during GET request to Azure Management API: {e}",
            status_code=500,
        )
    response_get.raise_for_status()
    logging.info(f"3. Sucessfully made GET request to Azure Management API. {response_get.status_code}")
    json_get = json.dumps(response_get.json(), indent=4)

    scopes = ["https://storage.azure.com/.default"]
    token = credential.get_token(*scopes)
    url = f"https://{st_name}.blob.core.windows.net/{cont_name}/README.md"
    file_content = f"""## This is a sample README file.
This file is stored in Azure Blob Storage.
~~~json
{json_get}
~~~
"""
    headers = {
        "Authorization": f"Bearer {token.token}",
        "x-ms-date": f"{now}",
        "Content-type": "application/octet-stream",
        "x-ms-version": "2020-04-08",
        "Accept": "application/octet-stream;odata=fullmetadata",
        "x-ms-blob-content-disposition": "attachment",
        "x-ms-blob-type": "BlockBlob",
    }
    try:
        response = requests.put(url, headers=headers, data=file_content)
    except Exception as e:
        logging.error(f"Error during PUT request to Azure Blob Storage: {e}")
        return func.HttpResponse(
            f"Error during PUT request to Azure Blob Storage: {e}",
            status_code=500,
        )
    response.raise_for_status()
    logging.info(f"4. Sucessfully made PUT request to Azure Blob Storage. {response.status_code}")
    json_modified = response_get.json()
    json_modified["status_code"] = response.status_code
    json_get = json.dumps(json_modified, indent=4)
    return func.HttpResponse(
        f"{json_get}",
        status_code=200,
    )
