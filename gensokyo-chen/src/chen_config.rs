use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::fs;

#[derive(Serialize, Deserialize)]
pub struct ChenConfig {
    #[serde(rename = "gensokyo_url")]
    pub gensokyo_url: String,
    #[serde(rename = "reconnect_timeout")]
    pub reconnect_timeout: i32,
    #[serde(rename = "client_secret")]
    pub client_secret: String,
    #[serde(rename = "jobs")]
    pub jobs: HashMap<String, ChenJob>,
}

#[derive(Serialize, Deserialize)]
pub struct ChenJob {
    #[serde(rename = "executable")]
    pub executable: String,
    #[serde(rename = "arguments")]
    pub arguments: Option<String>,
    #[serde(rename = "timeout")]
    pub timeout: i32,
    #[serde(rename = "async")]
    pub is_async: bool,
    #[serde(rename = "allowed_clients")]
    pub allowed_clients: Option<Vec<String>>,
}

impl ChenConfig {
    pub fn load(path: &str) -> ChenConfig {
        let config = fs::read_to_string(path).expect("Failed to read config file.");
        serde_json::from_str(&config).expect("Failed to parse config file.")
    }
}
