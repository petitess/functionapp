import azure.functions as func
import datetime
import logging
import os
import requests

app = func.FunctionApp()


@app.function_name(name="trigger-http-jitbit-01")
@app.route(route="trigger-http-jitbit-01", auth_level=func.AuthLevel.FUNCTION)
def http_trigger(req: func.HttpRequest) -> func.HttpResponse:

    hour = datetime.datetime.now().hour
    night_mode = hour < 6 or hour > 15
    token_grafana = os.environ.get("TOKEN_GRAFANA")
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
    query = ""
    ticket_cat = "Övervakning / Larm"
    ticket_id = None
    ticket_description = ""
    azure_alert_rule_ID = ""
    azure_alert_source_ID = ""
    azure_alert_description = ""
    zabbix_message = ""
    grafana_user_id = "12345"
    svc_aacount_id = "UAS86KT8JHABC"

    try:
        req_body = req.get_json()
    except ValueError:
        pass

    if isinstance(req_body, dict):
        alert_event_type = req_body.get("event", {}).get("type")
        alert_integration_type = req_body.get("integration", {}).get("type")
        alert_title = req_body.get("alert_group", {}).get("title")
        alert_group_id = req_body.get("alert_group_id")
        alert_user_email = "grafana@a3.se"
        user = req_body.get("user", {})
        if isinstance(user, dict):
            alert_user_email = req_body.get("user", {}).get("email", "grafana@a3.se")
        ticket_cat = (
            req_body.get("alert_group", {})
            .get("labels", {})
            .get("source", "Övervakning / Larm")
        )
        azure_alert_rule_ID = f"{req_body.get('alert_payload', {}).get('data', {}).get('essentials', {}).get('alertRuleID', '')}"
        azure_alert_source_ID = f"{req_body.get('alert_payload', {}).get('data', {}).get('alertContext', {}).get('sourceId', '')}"
        azure_alert_description = f"{req_body.get('alert_payload', {}).get('data', {}).get('essentials', {}).get('description', '')}"
        zabbix_message = req_body.get("alert_payload", {}).get("message", "")

        # safely navigate into nested alertContext.condition which may not always be a dict
        condition = (
            req_body.get("alert_payload", {})
            .get("data", {})
            .get("alertContext", {})
            .get("condition")
        )
        if not isinstance(condition, dict):
            logging.warning(f"EXPECTED condition dict but got: {type(condition)}")
            all_of = ""
        else:
            all_of = condition.get("allOf", "")

        if isinstance(all_of, list) and len(all_of) > 0 and isinstance(all_of[0], dict):
            query = all_of[0].get("linkToSearchResultsUI", "")
        else:
            query = ""
        ticket_description = (
            f"{url_grafana}/a/grafana-irm-app/alert-groups/{alert_group_id}\n"
            "---------------\n"
            f"{query}\n"
            "---------------\n"
            f"{zabbix_message  or azure_alert_rule_ID or azure_alert_source_ID or azure_alert_description}"
        )

    logging.warning(f"1.ALERT_TYPE: {alert_event_type}")
    logging.warning(f"2.ALERT_TITLE: {alert_title}")
    logging.warning(f"3.ALERT_ID: {alert_group_id}")
    logging.warning(f"4.ALERT_USER: {alert_user_email}")
    logging.warning(f"5.GRAFANA_TOKEN: {token_grafana[0:10]}")

    response_user_id = requests.get(
        f"{url_jitbit}/UserByEmail",
        params={"email": alert_user_email},
        headers=headers_jitbit,
        timeout=30,
    )

    user_id = response_user_id.json().get("UserID")
    user_id = 0 if user_id is None else user_id
    logging.warning(f"6.USER_ID: {user_id}")

    if alert_event_type == "alert group created" or alert_event_type == "escalation":
        logging.warning("ACTION: new alert group has been created")
        logging.warning(f"azure_alert_rule_ID: {azure_alert_rule_ID[:25]}")
        logging.warning(f"azure_alert_source_ID: {azure_alert_source_ID[:25]}")
        logging.warning(f"azure_alert_description: {azure_alert_description[:25]}")
        logging.warning(f"zabbix_message: {zabbix_message[:25]}")
        # resolution_notes = req_body.get("alert_group", {}).get("resolution_notes", [])
        if alert_integration_type == "zabbix" and night_mode:
            logging.warning("INTEGRATION: Zabbix alert received")
            response_resolution_note = requests.post(
                f"{url_grafana_oncall}/api/v1/resolution_notes/",
                headers=headers_grafana,
                json={
                    "alert_group_id": alert_group_id,
                    "text": "no_ticket_id",
                },
            )
            return func.HttpResponse(
                f"{alert_integration_type}. skipping ticket creation. {hour}"
            )

        response_cat = requests.get(
            f"{url_jitbit}/categories", headers=headers_jitbit
        )
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

        # if resolution_notes is None or len(resolution_notes) == 0:
        response_resolution_note = requests.post(
            f"{url_grafana_oncall}/api/v1/resolution_notes/",
            headers=headers_grafana,
            json={
                "alert_group_id": alert_group_id,
                "text": ticket_id,
            },
        )
        logging.warning(
            f"NOTE_ADDED: {response_resolution_note.ok}, {response_resolution_note.status_code}"
        )
        logging.warning(f"NOTE_URL: {response_resolution_note.url}")
        if not response_resolution_note.ok:
            return func.HttpResponse(
                f"NOTE_ADDED: {response_resolution_note.ok} , {response_resolution_note.status_code}",
                status_code=500,
            )
        # else:
        #     logging.warning(f"NOTE_ALREADY_EXISTS")

    elif alert_event_type == "acknowledge":
        resolution_notes = req_body.get("alert_group", {}).get("resolution_notes", [])
        resolution_notes = [
            note.get("text")
            for note in resolution_notes
            if isinstance(note, dict) and note.get("author") == svc_aacount_id
            # and re.match(r"^\d+$", str(note.get("text")))
        ]

        if not resolution_notes:
            logging.warning("No ticket IDs found")

        for ticket_id in resolution_notes:
            logging.warning(f"TICKET_ID: {ticket_id}")

            if ticket_id == "no_ticket_id":
                logging.warning(
                    "no_ticket_id found in resolution notes, skipping ticket update"
                )
                return func.HttpResponse(f"no_ticket_id found in resolution notes")

            response_ack = requests.post(
                f"{url_jitbit}/UpdateTicket",
                params={
                    "id": ticket_id,
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
                        "id": ticket_id,
                        "body": f"Alert was acknowledged by {alert_user_email}",
                        "forTechsOnly": True,
                        "recipientIds": grafana_user_id,
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
            if isinstance(note, dict) and note.get("author") == svc_aacount_id
            # and re.match(r"^\d+$", str(note.get("text")))
        ]

        if not resolution_notes:
            logging.warning("No ticket IDs found")

        for ticket_id in resolution_notes:
            logging.warning(f"TICKET_ID: {ticket_id}")

            if ticket_id == "no_ticket_id":
                logging.warning(
                    "no_ticket_id found in resolution notes, skipping ticket update"
                )
                return func.HttpResponse(f"no_ticket_id found in resolution notes")

            response_comment = requests.post(
                f"{url_jitbit}/comment",
                params={
                    "id": ticket_id,
                    "body": f"Alert was closed by {alert_user_email}",
                    "forTechsOnly": True,
                    "recipientIds": grafana_user_id,
                },
                headers=headers_jitbit,
            )
            logging.warning(f"8.COMMENT: {response_comment.ok}")

            response_resolve = requests.post(
                f"{url_jitbit}/Close",
                params={
                    "id": ticket_id,
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
            if isinstance(note, dict) and note.get("author") == svc_aacount_id
            # and re.match(r"^\d+$", str(note.get("text")))
        ]

        if not resolution_notes:
            logging.warning("No ticket IDs found")

        for ticket_id in resolution_notes:
            logging.warning(f"TICKET_ID: {ticket_id}")

            if ticket_id == "no_ticket_id":
                logging.warning(
                    "no_ticket_id found in resolution notes, skipping ticket update"
                )
                return func.HttpResponse(f"no_ticket_id found in resolution notes")

            response_unack = requests.post(
                f"{url_jitbit}/UpdateTicket",
                params={
                    "id": ticket_id,
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
                        "id": ticket_id,
                        "body": f"Alert was unacknowledged by {alert_user_email}",
                        "forTechsOnly": True,
                        "recipientIds": grafana_user_id,
                    },
                    headers=headers_jitbit,
                )
                logging.warning(f"8.COMMENT: {response_comment.ok}")

    elif alert_event_type == "silence":
        resolution_notes = req_body.get("alert_group", {}).get("resolution_notes", [])
        resolution_notes = [
            note.get("text")
            for note in resolution_notes
            if isinstance(note, dict) and note.get("author") == svc_aacount_id
            # and re.match(r"^\d+$", str(note.get("text")))
        ]

        if not resolution_notes:
            logging.warning("No ticket IDs found")

        for ticket_id in resolution_notes:
            logging.warning(f"TICKET_ID: {ticket_id}")

            if ticket_id == "no_ticket_id":
                logging.warning(
                    "no_ticket_id found in resolution notes, skipping ticket update"
                )
                return func.HttpResponse(f"no_ticket_id found in resolution notes")

            response_ack = requests.post(
                f"{url_jitbit}/UpdateTicket",
                params={
                    "id": ticket_id,
                    "assignedUserId": grafana_user_id,
                    "suppressNotifications": True,
                },
                headers=headers_jitbit,
            )
            logging.warning(f"8.SILENCED: {response_ack.ok}")
            if not response_ack.ok:
                return func.HttpResponse(
                    f"SILENCED: {response_ack.ok}",
                    status_code=500,
                )
    elif alert_event_type is not None:
        logging.warning(f"ACTION: unknown event type received: {alert_event_type}")

    if req_body is not None:
        # logging.warning(f"REQ_BODY: {req_body}")

        return func.HttpResponse(f"{alert_event_type} ready. {hour}")
    else:
        logging.warning(f"no json")
        return func.HttpResponse(
            f"no json",
            status_code=200,
        )
