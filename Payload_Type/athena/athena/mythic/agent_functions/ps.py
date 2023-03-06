from mythic_container.MythicCommandBase import *
import json


class PsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class PsCommand(CommandBase):
    cmd = "ps"
    needs_admin = False
    help_cmd = "ps"
    description = "Get a brief process listing with basic information."
    version = 1
    is_exit = False
    is_file_browse = False
    supported_ui_features = ["process_browser:list"]
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    is_process_list = True
    author = "@checkymander"
    argument_class = PsArguments
    #attackmapping = ["T1106"]
    attackmapping = []
    browser_script = BrowserScript(script_name="ps", author="@checkymander")
    attributes = CommandAttributes(
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass