use futures_util::{SinkExt, StreamExt};
use futures_util::stream::{SplitSink, SplitStream};
use tokio_tungstenite::{connect_async, MaybeTlsStream};
use log::{error, info};
use tokio_tungstenite::tungstenite::Message;
use tokio_tungstenite::tungstenite::protocol::CloseFrame;
use tokio_tungstenite::tungstenite::protocol::frame::coding::CloseCode;
use tokio_tungstenite::tungstenite::protocol::frame::coding::CloseCode::*;
use gethostname::gethostname;
use crate::chen_config::ChenConfig;
use crate::models;

type Socket = tokio_tungstenite::WebSocketStream<MaybeTlsStream<tokio::net::TcpStream>>;
type SocketTx<'a> = SplitSink<&'a mut Socket, Message>;
type SocketRx<'a> = SplitStream<&'a mut Socket>;

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
                            Self::on_receive_message(&mut tx, &mut rx, &message, config).await;
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

    async fn on_receive_message(tx: &mut SocketTx<'_>, rx: &mut SocketRx<'_>, message: &str, config: &ChenConfig) {
        let request: models::JobRequest = serde_json::from_str(&message).unwrap();
        let job = &config.jobs[&request.job_name];

        let response = models::JobResponse {
            job_id: request.job_id,
            success: false,
            is_async: job.is_async,
            result: None,
        };

        let response_message = serde_json::to_string(&response).unwrap();

        Self::send_message(tx, &response_message).await;
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
