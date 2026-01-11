from azure.identity import DefaultAzureCredential
from azure.mgmt.compute import ComputeManagementClient
from azure.mgmt.compute.models import RunCommandInput
import azure.functions as func
import logging
import requests
import datetime
import send_email
import os


def get_user_list():
    now = datetime.datetime.now().strftime("%a, %d %b %Y %H:%M:%S GMT")
    sub_id = os.environ.get("X_SUB_ID", "")
    sender = os.environ.get("X_SENDER", "passwordreminder@abcd.se")
    vm_rg_name = os.environ.get("X_VM_RG_NAME", "rg-vmmgmtprod01")
    vm_name = os.environ.get("X_VM_NAME", "vmmgmtprod01")
    st_name = os.environ.get("X_ST_NAME", "storageprodabcd01")
    table_name = os.environ.get("X_TABLE_NAME", "userpasswordexpiration")
    entities = []
    credential = DefaultAzureCredential()
    token_graph = credential.get_token("https://graph.microsoft.com/.default")
    logging.warning(f"1.token_graph: {token_graph.token[0:30]}")
    compute_client = ComputeManagementClient(credential, sub_id)

    run_command_input = RunCommandInput(
        command_id="RunPowerShellScript",
        script=["C:\Script\AdPasswordExpiration.ps1"],
    )

    poller = compute_client.virtual_machines.begin_run_command(
        resource_group_name=vm_rg_name, vm_name=vm_name, parameters=run_command_input
    )

    result = poller.result()

    for item in result.value:
        if item.message:
            logging.warning(f"2.run_command: {item.code}")
            logging.warning(f"3.sub: {item.message}")

    scopes = ["https://storage.azure.com/.default"]
    token_st = credential.get_token(*scopes)
    logging.warning(f"4.token_st: {token_st.token[0:30]}")
    url = f"https://{st_name}.table.core.windows.net/{table_name}"
    headers = {
        "Authorization": f"Bearer {token_st.token}",
        "x-ms-date": f"{now}",
        "x-ms-version": "2020-04-08",
        "Accept": "application/json;odata=fullmetadata",
    }
    response = requests.get(url, headers=headers)
    data = response.json()
    entities.extend(data.get("value", []))

    if response.status_code == 200:
        logging.warning(f"5.Fetched {len(entities)} entities.")
    else:
        logging.error(
            f"Could not fetch entities: {response.status_code} - {response.text}"
        )
        return func.HttpResponse(
            f"{response.text}",
            status_code=500,
        )

    for e in entities:
        headers["If-Match"] = "*"
        logging.warning(e["odata.id"])
        logging.warning(e["Mail"])
        send_email.send_email_via_graph(
            token_graph.token,
            sender,
            e["Mail"],
            e["ExpiryDate"],
            e["Name"],
        )
        response = requests.delete(e["odata.id"], headers=headers)
        if response.status_code == 204:
            logging.warning(f"6.Deleted entity.")
        else:
            logging.error(
                f"Could not delete entity: {response.status_code} - {response.text}"
            )
            return func.HttpResponse(
                f"{response.text}",
                status_code=500,
            )
