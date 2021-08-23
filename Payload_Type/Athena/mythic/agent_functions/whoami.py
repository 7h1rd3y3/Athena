from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class WhoamiArguments(TaskArguments):
    def __init__(self, command_line):
        super().__init__(command_line)
        self.args = {}

    async def parse_arguments(self):
        pass


class WhoamiCommand(CommandBase):
    cmd = "whoami"
    needs_admin = False
    help_cmd = "whoami"
    description = "Tasks Athena to display the current user context."
    version = 1
    supported_ui_features = ["callback_table:exit"]
    author = "@checkymander"
    attackmapping = []
    argument_class = WhoamiArguments
8
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        resp = await MythicRPC().execute("create_artifact", task_id=task.id,
            artifact="$.NSApplication.sharedApplication.terminate",
            artifact_type="API Called",
        )
        return task

    async def process_response(self, response: AgentResponse):
        pass

