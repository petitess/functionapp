import azure.functions as func
import datetime
import logging
import os
import requests

app = func.FunctionApp()


@app.function_name(name="trigger-http-jitbit-01")
@app.route(route="trigger-http-jitbit-01", auth_level=func.AuthLevel.FUNCTION)
def http_trigger(req: func.HttpRequest) -> func.HttpResponse:

    now = datetime.datetime.now().strftime("%a, %d %b %Y %H:%M:%S GMT")
    token_grafana = os.environ.get("TOKEN_GRAFANA", "")
    url_grafana_oncall = os.environ.get(
        "URL_GRAFANA_ONCALL", "https://oncall-prod-eu-west-0.grafana.net/oncall"
    )
    url_grafana = os.environ.get("URL_GRAFANA", "https://karolsek.grafana.net")
    headers_grafana = {
        "Authorization": f"{token_grafana}",
        "Content-Type": "application/json",
        "X-Grafana-URL": url_grafana,
    }
    url_jitbit = os.environ.get(
        "URL_JITBIT", "https://jitbitproduction.azurewebsites.net/api"
    )
    token_jitbit = os.environ.get("TOKEN_JITBIT", "")
    headers_jitbit = {
        "Authorization": f"Bearer {token_jitbit}",
        "Content-Type": "application/json",
    }

    req_body = None
    alert_event_type = None
    alert_title = None
    alert_group_id = None
    alert_description = ""
    query = ""
    ticket_cat = "Övervakning / Larm"
    ticket_id = None
    ticket_description = ""

    try:
        req_body = req.get_json()
    except ValueError:
        pass

    if isinstance(req_body, dict):
        alert_event_type = req_body.get("event", {}).get("type")
        alert_title = req_body.get("alert_group", {}).get("title")
        alert_group_id = req_body.get("alert_group_id")
        alert_user_email = "opsgenie@b3.se"
        user = req_body.get("user", {})
        if isinstance(user, dict):
            alert_user_email = req_body.get("user", {}).get("email", "opsgenie@b3.se")
        ticket_cat = (
            req_body.get("alert_group", {})
            .get("labels", {})
            .get("Cat", "Övervakning / Larm")
        )
        alertRuleID = f"{req_body.get('alert_payload', {}).get('data', {}).get('essentials', {}).get('alertRuleID', '')}"
        alert_description = (
            req_body.get("last_alert", {})
            .get("data", {})
            .get("essentials", {})
            .get("description", "")
        )

        all_of = (
            req_body.get("alert_payload", {})
            .get("data", {})
            .get("alertContext", {})
            .get("condition", {})
            .get("allOf")
        )
        if isinstance(all_of, list) and len(all_of) > 0 and isinstance(all_of[0], dict):
            query = all_of[0].get("linkToSearchResultsUI", "no_query")
        else:
            query = "no_query"
        ticket_description = (
            f"{url_grafana}/a/grafana-irm-app/alert-groups/{alert_group_id}\n"
            "---------------\n"
            f"{query}\n"
            "---------------\n"
            f"{alert_description or alertRuleID}"
        )

    logging.warning(f"1.ALERT_TYPE: {alert_event_type}")
    logging.warning(f"2.ALERT_TITLE: {alert_title}")
    logging.warning(f"3.ALERT_ID: {alert_group_id}")
    logging.warning(f"4.ALERT_DESCRIPTION: {alert_description}")
    logging.warning(f"5.ALERT_USER: {alert_user_email}")

    response_user_id = requests.get(
        f"{url_jitbit}/UserByEmail",
        params={"email": alert_user_email},
        headers=headers_jitbit,
        timeout=30,
    )

    user_id = response_user_id.json().get("UserID")
    user_id = 0 if user_id is None else user_id
    logging.warning(f"6.USER_ID: {user_id}")

    if alert_event_type == "alert group created":
        logging.warning("ACTION: new alert group has been created")

        response_cat = requests.get(f"{url_jitbit}/categories", headers=headers_jitbit)
        logging.warning(f"CATEGORY_LIST: {response_cat.ok}")

        category_id = next(
            (
                item.get("CategoryID")
                for item in response_cat.json()
                if item.get("NameWithSection") == ticket_cat
            ),
            537,
        )

        if category_id is None:
            logging.warning(f"CATEGORY NOT FOUND: {ticket_cat}")

        logging.warning(f"CATEGORY_ID: {category_id}")
        response_create = requests.post(
            f"{url_jitbit}/ticket",
            headers=headers_jitbit,
            params={
                "categoryId": category_id,
                "body": ticket_description,
                "subject": alert_title,
                "priorityId": 0,
                "userId": user_id,
            },
        )
        logging.warning(
            f"TICKET_CREATED: {response_create.ok}, {response_create.json()}"
        )
        ticket_id = response_create.json()

        response_field = requests.post(
            f"{url_jitbit}/SetCustomField",
            headers=headers_jitbit,
            params={
                "ticketId": ticket_id,
                "fieldId": 10,
                "value": "Event",
            },
        )
        logging.warning(f"FIELD_UPDATED: {response_field.ok}")

        response_resolution_note = requests.post(
            f"{url_grafana_oncall}/api/v1/resolution_notes/",
            headers=headers_grafana,
            json={
                "alert_group_id": alert_group_id,
                "text": ticket_id,
            },
        )
        logging.warning(f"NOTE_ADDED: {response_resolution_note.ok}")
        if not response_resolution_note.ok:
            return func.HttpResponse(
                f"NOTE_ADDED: {response_resolution_note.ok}",
                status_code=500,
            )

    elif alert_event_type == "acknowledge":
        resolution_notes = req_body.get("alert_group", {}).get("resolution_notes", [])
        resolution_notes = [
            note.get("text")
            for note in resolution_notes
            if isinstance(note, dict) and note.get("author") == None
            # and re.match(r"^\d+$", str(note.get("text")))
        ]
        logging.warning(f"TICKET_ID: {resolution_notes[0]}")

        response_ack = requests.post(
            f"{url_jitbit}/UpdateTicket",
            params={
                "id": resolution_notes[0],
                "assignedUserId": user_id,
                "suppressNotifications": True,
            },
            headers=headers_jitbit,
        )
        logging.warning(f"8.ACKNOWLEDGED: {response_ack.ok}")
        if not response_ack.ok:
            return func.HttpResponse(
                f"ACKNOWLEDGED: {response_ack.ok}",
                status_code=500,
            )
        else:
            response_comment = requests.post(
                f"{url_jitbit}/comment",
                params={
                    "id": resolution_notes[0],
                    "body": f"Alert was acknowledged by {alert_user_email}",
                    "forTechsOnly": True,
                    "recipientIds": None,
                },
                headers=headers_jitbit,
            )
            logging.warning(f"8.COMMENT: {response_comment.ok}")

    elif alert_event_type == "resolve":
        logging.warning("ACTION: alert has been resolved")
        resolution_notes = req_body.get("alert_group", {}).get("resolution_notes", [])
        resolution_notes = [
            note.get("text")
            for note in resolution_notes
            if isinstance(note, dict) and note.get("author") == None
            # and re.match(r"^\d+$", str(note.get("text")))
        ]
        logging.warning(f"TICKET_ID: {resolution_notes[0]}")

        response_comment = requests.post(
            f"{url_jitbit}/comment",
            params={
                "id": resolution_notes[0],
                "body": f"Alert was closed by {alert_user_email}",
                "forTechsOnly": True,
                "recipientIds": None,
            },
            headers=headers_jitbit,
        )
        logging.warning(f"8.COMMENT: {response_comment.ok}")

        response_resolve = requests.post(
            f"{url_jitbit}/Close?id={resolution_notes[0]}&suppressNotification=true",
            params={
                "id": resolution_notes[0],
                "suppressNotification": True,
            },
            headers=headers_jitbit,
        )
        logging.warning(f"8.CLOSED: {response_resolve.ok}")
        if not response_resolve.ok:
            return func.HttpResponse(
                f"CLOSED: {response_resolve.ok}",
                status_code=500,
            )

    elif alert_event_type == "unacknowledge":
        logging.warning("ACTION: alert has been unacknowledged")
        resolution_notes = req_body.get("alert_group", {}).get("resolution_notes", [])
        resolution_notes = [
            note.get("text")
            for note in resolution_notes
            if isinstance(note, dict) and note.get("author") == None
            # and re.match(r"^\d+$", str(note.get("text")))
        ]
        logging.warning(f"TICKET_ID: {resolution_notes[0]}")

        response_unack = requests.post(
            f"{url_jitbit}/UpdateTicket",
            params={
                "id": resolution_notes[0],
                "assignedUserId": 0,
                "suppressNotifications": True,
            },
            headers=headers_jitbit,
        )
        logging.warning(f"8.UNACKNOWLEDGED: {response_unack.ok}")
        if not response_unack.ok:
            return func.HttpResponse(
                f"UNACKNOWLEDGED: {response_unack.ok}",
                status_code=500,
            )
        else:
            response_comment = requests.post(
                f"{url_jitbit}/comment",
                params={
                    "id": resolution_notes[0],
                    "body": f"Alert was unacknowledged by {alert_user_email}",
                    "forTechsOnly": True,
                    "recipientIds": None,
                },
                headers=headers_jitbit,
            )
            logging.warning(f"8.COMMENT: {response_comment.ok}")

    elif alert_event_type is not None:
        logging.warning(f"ACTION: unknown event type received: {alert_event_type}")

    if req_body is not None:
        # logging.warning(f"REQ_BODY: {req_body}")

        return func.HttpResponse(f"{alert_event_type} ready")
    else:
        logging.warning(f"no json")
        return func.HttpResponse(
            f"no json",
            status_code=200,
        )
