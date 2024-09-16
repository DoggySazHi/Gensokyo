use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize)]
pub enum ConnectionReason {
    Success,
    InvalidKey,
    InvalidName,
    InvalidJobs,
    InvalidPayload,
}

#[derive(Serialize, Deserialize)]
pub struct ConnectionRequest {
    pub client_secret: Option<String>,
    pub friendly_name: Option<String>,
    pub jobs_available: Option<Vec<String>>,
}

#[derive(Serialize, Deserialize)]
pub struct ConnectionResponse {
    pub success: bool,
    pub reason: ConnectionReason,
}

#[derive(Serialize, Deserialize)]
pub struct Heartbeat {
    pub timestamp: String,
    pub acknowledged: Option<String>,
}

#[derive(Serialize, Deserialize)]
pub struct JobRequest {
    pub job_id: String,
    pub job_name: String,
    pub job_data: String,
    pub client_name: String,
}

#[derive(Serialize, Deserialize)]
pub struct JobResponse {
    pub job_id: String,
    pub success: bool,
    #[serde(rename = "async")]
    pub is_async: bool,
    pub result: Option<String>,
}
