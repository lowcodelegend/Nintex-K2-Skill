"""Case-type runtime plugin template for the reusable MCP server.

Copy this file into the solution repository and replace every example provider.
The MCP server imports only the explicitly configured file and factory function.
"""


def create_runtime(settings, contract_registry):
    del settings, contract_registry
    raise RuntimeError(
        "Replace this template with durable draft, governed lookup/file, and "
        "atomic case-creation adapter providers before enabling mutations."
    )

    # Return this exact shape after implementing the providers:
    # return {
    #     "lookupProvider": lookup_provider,
    #     "fileHandleProvider": file_handle_provider,
    #     "draftStore": durable_draft_store,
    #     "durableDrafts": True,
    #     "adapters": {
    #         "RQB.Case.CreateFromIntake": rqb_creation_adapter,
    #     },
    # }
