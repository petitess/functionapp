# Prereq:
# Application.ReadWrite.OwnedBy for the Function App
# Key Vault Secrets Officer for the Function App
import json
import azure.functions as func
from azure.identity import DefaultAzureCredential
import logging
import os
import requests
import datetime

app = func.FunctionApp()


@app.route(route="secret_rotator", auth_level=func.AuthLevel.ANONYMOUS)
def secret_rotator(req: func.HttpRequest) -> func.HttpResponse:
    logging.warning("1. Python HTTP trigger function processed a request.")

    now = datetime.datetime.now().strftime("%Y%m%d-%H%M")
    kv_url = os.environ.get("X_KV_URL", f"https://kv-python.vault.azure.net/")
    appRegName = os.environ.get("X_APP_REG_NAME", "sp-mg-tenant-root-group")
    appRegObjectId = os.environ.get(
        "X_APP_REG_OBJECT_ID", "8cffb11c-ff12-432a-911b-c7f5a1e39cfe"
    )
    credential = DefaultAzureCredential()
    logging.warning("2. Using DefaultAzureCredential for authentication.")
    scopes = ["https://graph.microsoft.com/.default"]
    token_graph = credential.get_token(*scopes)
    logging.warning(f"3.token_graph: {token_graph.token[0:30]}")
    headers = {
        "Authorization": f"Bearer {token_graph.token}",
        "Content-Type": "application/json",
    }
    url = f"https://graph.microsoft.com/v1.0/applications/{appRegObjectId}"
    response_graph = requests.get(url, headers=headers)

    url = (
        f"https://graph.microsoft.com/v1.0/applications/{appRegObjectId}/removePassword"
    )
    for cred in response_graph.json().get("passwordCredentials"):
        body = {"keyId": cred["keyId"]}
        response_graph = requests.post(url, headers=headers, json=body)
        if response_graph.status_code == 204:
            logging.warning(f"4.removed: {cred["displayName"]}")
        else:
            logging.error(
                f"Could not remove secret: {response_graph.status_code} - {response_graph.text}"
            )
            return func.HttpResponse(
                f"{response_graph.text}",
                status_code=500,
            )

    url = f"https://graph.microsoft.com/v1.0/applications/{appRegObjectId}/addPassword"
    body = {
        "passwordCredential": {
            "displayName": f"secret_{now}",
            "endDateTime": (
                datetime.datetime.now() + datetime.timedelta(days=3650)
            ).isoformat()
            + "Z",
        }
    }
    response_graph = requests.post(url, headers=headers, data=json.dumps(body))
    logging.warning(f"5.hint: {response_graph.json().get('hint')}")

    if response_graph.status_code == 200:
        logging.warning(f"6.Created a new secret.")
    else:
        logging.error(
            f"Could not create secret: {response_graph.status_code} - {response_graph.text}"
        )
        return func.HttpResponse(
            f"{response_graph.text}",
            status_code=500,
        )
    response_graph.raise_for_status()
    json_graph = json.dumps(response_graph.json(), indent=4)

    scopes = ["https://vault.azure.net/.default"]
    token_kv = credential.get_token(*scopes)
    logging.warning(f"7.token_kv: {token_kv.token[0:30]}")
    url = f"{kv_url}secrets/{appRegName}?api-version=2025-07-01"
    headers = {
        "Authorization": f"Bearer {token_kv.token}",
        "Content-Type": "application/json",
    }
    body = {
        "value": response_graph.json().get("secretText"),
    }
    response_kv = requests.put(url, headers=headers, data=json.dumps(body))
    if response_kv.status_code == 200:
        logging.warning(f"8.Created a new key vault secret.")
    else:
        logging.error(
            f"Could not create key vault secret: {response_kv.status_code} - {response_kv.text}"
        )
        return func.HttpResponse(
            f"{response_kv.text}",
            status_code=500,
        )
    response_kv.raise_for_status()

    return func.HttpResponse(
        f"{json_graph}",
        status_code=200,
    )
