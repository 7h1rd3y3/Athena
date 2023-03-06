from mythic_container.MythicCommandBase import *
import json


class TimestompArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="source",
                type=ParameterType.String,
                description="Source file to get timestamp information from",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
            CommandParameter(
                name="destination",
                type=ParameterType.String,
                description="Destination file to apply the timestamp to",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("source", self.command_line.split()[0])
                self.add_arg("destination", self.command_line.split()[1])
        else:
            raise ValueError("Missing arguments")


class TimestompCommand(CommandBase):
    cmd = "timestomp"
    needs_admin = False
    help_cmd = "timestomp <source> <destination>"
    description = "Match the timestamp of a source file to the timestamp of a destination file"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@checkymander"
    argument_class = TimestompArguments
    attackmapping = []
    attributes = CommandAttributes(
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass