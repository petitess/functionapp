import azure.functions as func
import datetime
import logging
import get_user_list

app = func.FunctionApp()

@app.timer_trigger(schedule="0 30 7 * * 1-5", arg_name="timer", run_on_startup=False,
              use_monitor=False) 
def password_expiration(timer: func.TimerRequest) -> None:
    
    if timer.past_due:
        logging.warning('The timer is past due!')

    logging.warning('Python timer trigger function executed: password_expiration')
    logging.warning(datetime.datetime.now().strftime("%Y-%m-%d-%H-%M"))
    get_user_list.get_user_list()