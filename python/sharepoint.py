#requirements.txt
#azure-functions
#azure-identity
#paramiko
import azure.functions as func
import json
import os
import logging
import requests
from azure.identity import ClientSecretCredential
import paramiko

app = func.FunctionApp()


@app.route(route="sharepoint_files_to_sftp", auth_level=func.AuthLevel.ANONYMOUS)
def sharepoint_files_to_sftp(req: func.HttpRequest) -> func.HttpResponse:
    logging.info("Python HTTP trigger function processed a request.")
    client_id = os.environ.get("az_client_id")
    client_secret = os.environ.get("az_client_secret")
    tenant_id = os.environ.get("az_client_tenant")
    graph_url = os.environ.get("MSGRAPH_URL", "https://graph.microsoft.com/v1.0")
    site_name = os.environ.get("SHP_SITE")
    document_name = os.environ.get("SHP_DOCUMENT", "SYSTEM")
    folder_name = os.environ.get("SHP_FOLDER")
    archive_folder_name = os.environ.get("SHP_FOLDER_ARCHIVE")
    sftp_host = os.environ.get("SFTP_HOST", "stsftpsharepoint.blob.core.windows.net")
    sftp_port = int(os.environ.get("SFTP_PORT", 22))
    sftp_user = os.environ.get("SFTP_USER", "stsftpsharepoint.user")
    sftp_pass = os.environ.get("SFTP_PASS")

    credential = ClientSecretCredential(
        client_id=client_id, client_secret=client_secret, tenant_id=tenant_id
    )

    files_moved = []
    files_not_moved = []

    token_graph = credential.get_token("https://graph.microsoft.com/.default")
    logging.warning(f"1.token_graph: {token_graph.token[0:30]}")
    headers = {
        "Authorization": f"Bearer {token_graph.token}",
        "Content-Type": "application/json",
    }

    site_resp = requests.get(
        f"{graph_url}/sites/root:/sites/{site_name}", headers=headers
    )
    site_id = site_resp.json().get("id")
    logging.warning(f"2.site_id: {site_id}")

    drives_resp = requests.get(f"{graph_url}/sites/{site_id}/drives", headers=headers)
    drives = drives_resp.json().get("value", [])
    doc_id = next(
        (drive.get("id") for drive in drives if drive.get("name") == document_name),
        None,
    )

    logging.warning(f"3.doc_id: {doc_id}")
    folder_resp = requests.get(
        f"{graph_url}/sites/{site_id}/drives/{doc_id}/items/root/children?$filter=name eq '{folder_name}'",
        headers=headers,
    )
    folders = folder_resp.json().get("value", [])
    folder_id = next((folder.get("id") for folder in folders), None)
    logging.warning(f"3.folder_id: {folder_id}")
    new_folder_resp = requests.get(
        f"{graph_url}/sites/{site_id}/drives/{doc_id}/items/root/children?$filter=name eq '{archive_folder_name}'",
        headers=headers,
    )
    new_folders = new_folder_resp.json().get("value", [])
    archive_folder_id = next((new_folder.get("id") for new_folder in new_folders), None)
    logging.warning(f"4.archive_folder_id: {archive_folder_id}")
    file_resp = requests.get(
        f"{graph_url}/sites/{site_id}/drives/{doc_id}/items/{folder_id}/children",
        headers=headers,
    )
    files = file_resp.json().get("value", [])
    logging.warning(f"5.files: {files}")

    for f in files:
        download_url = f.get("@microsoft.graph.downloadUrl")
        name = f.get("name")
        id = f.get("id")

        if not download_url:
            continue

        r = requests.get(download_url, stream=True)
        r.raise_for_status()

        transport = paramiko.Transport((sftp_host, sftp_port))
        transport.connect(username=sftp_user, password=sftp_pass)
        sftp = paramiko.SFTPClient.from_transport(transport)

        remote_path = f"/{name}"
        with sftp.open(remote_path, "wb") as remote_fh:
            for chunk in r.iter_content(chunk_size=1024 * 1024):
                if chunk:
                    remote_fh.write(chunk)

        sftp.close()
        transport.close()

        logging.info(f"uploaded to sftp: {remote_path}")
        body_move = {
            "name": f"old_{name}",
            "parentReference": {"id": archive_folder_id},
        }
        moved = requests.patch(
            url=f"{graph_url}/sites/{site_id}/drives/{doc_id}/items/{id}",
            headers=headers,
            json=body_move,
        )
        if moved.ok:
            files_moved.append(name)
            logging.info(f"moved: {name}")
        else:
            files_not_moved.append(name)
            logging.info(f"error moving: {moved.content}")

    response = {
        "token_graph": token_graph.token[0:30],
        "site_id": site_id,
        "doc_id": doc_id,
        "folder_id": folder_id,
        "archive_folder_id": archive_folder_id,
        "files_moved": files_moved,
        "files_not_moved": files_not_moved,
    }

    return func.HttpResponse(json.dumps(response, indent=4))
