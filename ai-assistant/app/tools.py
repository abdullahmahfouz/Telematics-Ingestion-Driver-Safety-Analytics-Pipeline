import json
from typing import Any

from app.telematics_client import telematics_client

TOOL_SCHEMAS: list[dict[str, Any]] = [
    {
        "type": "function",
        "function": {
            "name": "get_recent_records",
            "description": "Get the most recent telemetry readings for a device, newest first.",
            "parameters": {
                "type": "object",
                "properties": {
                    "device_id": {"type": "string", "description": "The device ID, e.g. b2A83F1"},
                    "limit": {"type": "integer", "description": "How many readings to return", "default": 10},
                },
                "required": ["device_id"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_harsh_braking_count",
            "description": "Count how many harsh-braking events a device has had within a trailing time window.",
            "parameters": {
                "type": "object",
                "properties": {
                    "device_id": {"type": "string", "description": "The device ID, e.g. b2A83F1"},
                    "since_hours": {
                        "type": "integer",
                        "description": "Lookback window in hours",
                        "default": 24,
                    },
                },
                "required": ["device_id"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_harsh_cornering_count",
            "description": "Count how many harsh-cornering events a device has had within a trailing time window.",
            "parameters": {
                "type": "object",
                "properties": {
                    "device_id": {"type": "string", "description": "The device ID, e.g. b2A83F1"},
                    "since_hours": {
                        "type": "integer",
                        "description": "Lookback window in hours",
                        "default": 24,
                    },
                },
                "required": ["device_id"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_harsh_acceleration_count",
            "description": "Count how many harsh-acceleration events a device has had within a trailing time window.",
            "parameters": {
                "type": "object",
                "properties": {
                    "device_id": {"type": "string", "description": "The device ID, e.g. b2A83F1"},
                    "since_hours": {
                        "type": "integer",
                        "description": "Lookback window in hours",
                        "default": 24,
                    },
                },
                "required": ["device_id"],
            },
        },
    },
]


async def call_tool(name: str, arguments: dict[str, Any], auth_token: str | None = None) -> str:
    """Executes one tool call and returns its result as a JSON string, ready to
    hand back to the model as a tool message."""
    if name == "get_recent_records":
        result = await telematics_client.get_recent_records(
            device_id=arguments["device_id"], limit=arguments.get("limit", 10), auth_token=auth_token
        )
    elif name == "get_harsh_braking_count":
        result = await telematics_client.get_harsh_braking_count(
            device_id=arguments["device_id"], since_hours=arguments.get("since_hours", 24), auth_token=auth_token
        )
    elif name == "get_harsh_cornering_count":
        result = await telematics_client.get_harsh_cornering_count(
            device_id=arguments["device_id"], since_hours=arguments.get("since_hours", 24), auth_token=auth_token
        )
    elif name == "get_harsh_acceleration_count":
        result = await telematics_client.get_harsh_acceleration_count(
            device_id=arguments["device_id"], since_hours=arguments.get("since_hours", 24), auth_token=auth_token
        )
    else:
        return json.dumps({"error": f"Unknown tool: {name}"})

    return json.dumps(result)
