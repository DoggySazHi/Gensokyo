use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize)]
enum ConnectionReason {
    Success,
    InvalidKey,
    InvalidName,
    InvalidJobs,
    InvalidPayload,
}

#[derive(Serialize, Deserialize)]
struct ConnectionRequest {
    client_secret: Option<String>,
    friendly_name: Option<String>,
    jobs_available: Option<Vec<String>>,
}

#[derive(Serialize, Deserialize)]
struct ConnectionResponse {
    success: bool,
    reason: ConnectionReason,
}

#[derive(Serialize, Deserialize)]
struct Heartbeat {
    timestamp: String,
    acknowledged: Option<String>,
}

#[derive(Serialize, Deserialize)]
struct JobRequest {
    job_id: String,
    job_name: String,
    job_data: String,
    client_name: String,
}

#[derive(Serialize, Deserialize)]
struct JobResponse {
    job_id: String,
    success: bool,
    #[serde(rename = "async")]
    is_async: bool,
    result: Option<String>,
}
