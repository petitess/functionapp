import requests
import logging
import os

def send_email_via_graph(access_token, sender, recipient, expiry_date, user_name):    
    graph_url = f"https://graph.microsoft.com/v1.0/users/{sender}/sendMail"
    recipient_temp = os.environ.get("X_RECIPIENT_TEMP", "")
    headers = {
        'Authorization': f'Bearer {access_token}',
        'Content-Type': 'application/json'
    }
    email_data = {
        "message": {
            "subject": "Ditt lösenord håller på att gå ut",
            "body": {
                "contentType": "text",
                "content": f"""Hej!

Lösenordet för nedanstående konto kommer att gå ut {expiry_date}
Användare: {recipient} ({user_name})

- För att byta ditt lösenord på en AAbcddator, tryck på Ctrl+Alt+Delete på tangentbordet och välj Ändra lösenord.
- Om du inte har en AAbcddator, logga in på https://myaccount.microsoft.com/  och välj Lösenord/byt Lösenord.
- Om du glömt ditt lösenord kan du återställa det genom att gå in på https://passwordreset.microsoftonline.com/ och följa instruktionerna.

Ditt nya lösenord måste uppfylla följande kriterier:
- Det måste bestå av minst 10 tecken (gärna längre)
- Lösenordet måste vara komplext, tex. innehålla tecken från tre av följande kategorier:
    - Stora bokstäver (A till Z)
    - Små bokstäver (a till z)
    - Siffror (0 till 9)
    - Icke alfanumeriska tecken (tex.:, !, $, #, %)

Första gången efter lösenordsbytet, då du med AAbcddator ansluter till Remote desktop och "O: koncerngemensam" kommer du bli ombedd att ange dina uppgifter på nytt. Tryck då "fler alternativ" och välj ditt användarkonto samt skriv in det nya lösenordet.

Vid problem eller frågor kontakta B3 Servicedesk på:

Telefon: 123123123
Mail: support.abcd@defg.se
Öppettider: Vardagar 07.00 - 18.00

Med vänliga hälsningar
AAbcd
"""
            },
            "toRecipients": [
                {
                    "emailAddress": {
                        "address": recipient if recipient_temp == "" else recipient_temp
                    }
                }
            ]
        },
        "saveToSentItems": "true"
    }
    try:
        response = requests.post(graph_url, headers=headers, json=email_data)
    except Exception as e:
        logging.error(f"Error during POST request to https://graph.microsoft.com/v1.0/users/{sender}/sendMail: {e}")
        raise Exception("Error") 

    if response.status_code == 202:
        logging.warning("✓ Email sent successfully via Graph API!")
    else:
        logging.error(f"✗ Could not send email: {response.status_code} - {response.text}")
        raise Exception("Error") 

