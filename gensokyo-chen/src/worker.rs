use futures_util::{SinkExt, StreamExt};
use futures_util::stream::{SplitSink};
use tokio_tungstenite::{connect_async, MaybeTlsStream};
use log::{debug, error, info, warn};
use tokio_tungstenite::tungstenite::Message;
use tokio_tungstenite::tungstenite::protocol::CloseFrame;
use tokio_tungstenite::tungstenite::protocol::frame::coding::CloseCode;
use tokio_tungstenite::tungstenite::protocol::frame::coding::CloseCode::*;
use gethostname::gethostname;
use tokio::process::Command;
use crate::chen_config::ChenConfig;
use crate::models;

type Socket = tokio_tungstenite::WebSocketStream<MaybeTlsStream<tokio::net::TcpStream>>;
type SocketTx<'a> = SplitSink<&'a mut Socket, Message>;

pub struct Worker {
    config: ChenConfig,
}

impl Worker {
    pub fn new(config: ChenConfig) -> Worker {
        Worker {
            config,
        }
    }

    pub async fn start(&mut self) {
        match connect_async(&self.config.gensokyo_url).await {
            Ok((mut stream, _)) => {
                println!("Connected to Gensokyo.");
                Self::run_loop(&self.config, &mut stream).await;
            }
            Err(e) => {
                println!("Failed to connect to Gensokyo: {:?}", e);
            }
        }
    }

    async fn run_loop(config: &ChenConfig, socket: &mut Socket) {
        info!("Worker started at: {}", chrono::Utc::now());

        let (mut tx, mut rx) = socket.split();

        Self::send_connection_request(&mut tx, config).await;

        loop {
            // Read from the WebSocket
            match rx.next().await {
                Some(Ok(msg)) => {
                    info!("Received message: {:?}", msg);

                    match msg {
                        Message::Text(message) => {
                            if let Err(ex) = Self::on_receive_message(&mut tx, &message, config).await {
                                error!("Error processing message: {:?}", ex);
                                break;
                            }
                        }
                        Message::Close(reason) => {
                            info!("WebSocket closed by server with reason: {:?}", reason);
                            break;
                        }
                        _ => {
                            error!("Received unsupported message type.");
                            break;
                        }
                    }
                }
                Some(Err(e)) => {
                    error!("Failed to receive message: {:?}", e);
                    break;
                }
                None => {
                    error!("WebSocket closed unexpectedly");
                    break;
                }
            }
        }

        info!("Worker stopping at: {}", chrono::Utc::now());

        Self::close_socket(&mut tx, Normal, "Worker stopped.").await;
    }

    async fn on_receive_message(tx: &mut SocketTx<'_>, message: &str, config: &ChenConfig) -> Result<(), String> {
        let request: models::JobRequest = serde_json::from_str(message).unwrap();

        match request.job_name.as_str() {
            "connection_response" => {
                Self::on_connection_response(&request, tx).await
            }
            "heartbeat" => {
                Self::on_heartbeat(&request, tx).await
            }
            "job_request" => {
                Self::on_job_request(&request, tx, config).await
            }
            "close" => {
                Self::close_socket(tx, Normal, "Server requested close.").await;
                Err("Server requested close, stopping worker.".to_string())
            }
            _ => {
                error!("Received unsupported message type.");
                Self::close_socket(tx, Invalid, "Unsupported message type.").await;
                Err("Unsupported message type.".to_string())
            }
        }
    }

    async fn on_connection_response(request: &models::JobRequest, tx: &mut SocketTx<'_>) -> Result<(), String> {
        let response: Result<models::ConnectionResponse, serde_json::error::Error> = serde_json::from_str(&request.job_data);

        if let Err(e) = response {
            error!("Failed to parse connection response: {:?}", e);
            return Err("Failed to parse connection response.".to_string());
        }

        let response = response.unwrap();

        if response.success {
            info!("Connection successful.");
            return Ok(());
        }

        error!("Connection failed: {:?}", response.reason.to_string());
        Self::close_socket(tx, Invalid, "Connection failed.").await;
        Err("Connection failed.".to_string())
    }

    async fn on_heartbeat(request: &models::JobRequest, tx: &mut SocketTx<'_>) -> Result<(), String> {
        let heartbeat: Result<models::Heartbeat, serde_json::error::Error> = serde_json::from_str(&request.job_data);

        if let Err(e) = heartbeat {
            error!("Failed to parse heartbeat: {:?}", e);
            return Err("Failed to parse heartbeat.".to_string());
        }

        let heartbeat = heartbeat.unwrap();

        debug!("Received heartbeat sent at {}", heartbeat.timestamp);

        let response = models::Heartbeat {
            timestamp: heartbeat.timestamp,
            acknowledged: Some(chrono::Utc::now().to_rfc3339()),
        };

        let message = serde_json::to_string(&response).unwrap();

        // Wrap the heartbeat in a JobResponse

        let response = models::JobResponse {
            job_id: request.job_id.clone(),
            success: true,
            is_async: false,
            result: Some(message),
        };

        let message = serde_json::to_string(&response).unwrap();

        Self::send_message(tx, &message).await;

        Ok(())
    }
    
    async fn on_job_request(request: &models::JobRequest, tx: &mut SocketTx<'_>, config: &ChenConfig) -> Result<(), String> {
        let job: Result<models::JobRequest, serde_json::error::Error> = serde_json::from_str(&request.job_data);
        
        if let Err(e) = job {
            error!("Failed to parse job request: {:?}", e);
            return Err("Failed to parse job request.".to_string());
        }
        
        let job = job.unwrap();
        
        info!("Received job request: {:?}", job.job_name);
        
        if !config.jobs.contains_key(&job.job_name) {
            warn!("Job not found: {:?}", job.job_name);
            
            let response = models::JobResponse {
                job_id: request.job_id.clone(),
                success: false,
                is_async: false,
                result: Some("Job not found".to_string()),
            };
            
            let message = serde_json::to_string(&response).unwrap();
            
            Self::send_message(tx, &message).await;
            return Ok(()); // Return early
        }
        
        let job_config = config.jobs.get(&job.job_name).unwrap();
        
        if job_config.allowed_clients.is_some() && !job_config.allowed_clients.as_ref().unwrap().contains(&request.client_name) {
            warn!("Client {} not allowed to run job {}", &request.client_name, &job.job_name);
            
            let response = models::JobResponse {
                job_id: request.job_id.clone(),
                success: false,
                is_async: false,
                result: Some("Client not allowed".to_string()),
            };
            
            let message = serde_json::to_string(&response).unwrap();
            
            Self::send_message(tx, &message).await;
            return Ok(()); // Return early
        }
        
        // Start process
        let command = Command::new(&job_config.executable)
            .args(job_config.arguments.as_deref().unwrap_or_default().split_whitespace())
            .env("GENSOKYO_JOB_ID", &job.job_id)
            .env("GENSOKYO_JOB_NAME", &job.job_name)
            .env("GENSOKYO_CLIENT_NAME", &request.client_name)
            .env("GENSOKYO_JOB_DATA", &job.job_data)
            .spawn();
        
        match command { 
            Ok(child) => {
                if job_config.is_async {
                    let response = models::JobResponse {
                        job_id: request.job_id.clone(),
                        success: true,
                        is_async: true,
                        result: None,
                    };
                    
                    let message = serde_json::to_string(&response).unwrap();
                    
                    Self::send_message(tx, &message).await;
                } else {
                    // Timeouts are not supported yet
                    let output = child.wait_with_output().await.unwrap();

                    let response = models::JobResponse {
                        job_id: request.job_id.clone(),
                        success: output.status.success(),
                        is_async: job_config.is_async,
                        result: Some(String::from_utf8_lossy(&output.stdout).to_string()),
                    };

                    let message = serde_json::to_string(&response).unwrap();

                    Self::send_message(tx, &message).await;
                }
                
                Ok(())
            }
            Err(ex) => {
                error!("Failed to start job: {:?}", ex);
                
                let response = models::JobResponse {
                    job_id: request.job_id.clone(),
                    success: false,
                    is_async: job_config.is_async,
                    result: Some("Failed to start job".to_string()),
                };
                
                let message = serde_json::to_string(&response).unwrap();
                
                Self::send_message(tx, &message).await;
                Ok(())
            }
        }
    }

    async fn send_connection_request(tx: &mut SocketTx<'_>, config: &ChenConfig) {
        let request = models::ConnectionRequest {
            client_secret: Some(config.client_secret.clone()),
            friendly_name: Some(gethostname().into_string().unwrap()),
            jobs_available: Some(config.jobs.keys().cloned().collect()),
        };

        let message = serde_json::to_string(&request).unwrap();

        Self::send_message(tx, &message).await;
    }

    async fn send_message(tx: &mut SocketTx<'_>, message: &str) {
        if let Err(e) = tx.send(Message::Text(message.to_string())).await {
            error!("Failed to send message: {:?}", e);
        }
    }

    async fn close_socket(tx: &mut SocketTx<'_>, close_code: CloseCode, message: &'static str) {
        if let Err(something) = tx.send(Message::Close(Some(CloseFrame {
            code: close_code,
            reason: message.into(),
        }))).await {
            error!("Failed to close WebSocket: {:?}", something);
        }
    }
}
