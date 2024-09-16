use futures_util::{SinkExt, StreamExt};
use futures_util::stream::SplitSink;
use tokio_tungstenite::{connect_async, MaybeTlsStream};
use log::{error, info};
use tokio_tungstenite::tungstenite::Message;
use tokio_tungstenite::tungstenite::protocol::CloseFrame;
use tokio_tungstenite::tungstenite::protocol::frame::coding::CloseCode;
use tokio_tungstenite::tungstenite::protocol::frame::coding::CloseCode::Normal;
use crate::chen_config::ChenConfig;

type Socket = tokio_tungstenite::WebSocketStream<MaybeTlsStream<tokio::net::TcpStream>>;
type SocketTx<'a> = SplitSink<&'a mut Socket, Message>;

pub struct Worker {
    config: ChenConfig,
}

impl Worker {
    pub(crate) fn new(config: ChenConfig) -> Worker {
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

        Self::send_message(&mut tx, "Hello, Gensokyo!").await;

        loop {
            // Read from the WebSocket
            match rx.next().await {
                Some(Ok(msg)) => {
                    info!("Received message: {:?}", msg);
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

    async fn send_message(tx: &mut SocketTx<'_>, message: &str) {
        if let Err(e) = tx.send(Message::Text(message.to_string())).await {
            error!("Failed to send message: {:?}", e);
        }
    }

    async fn close_socket(tx: &mut SocketTx<'_>, close_code: CloseCode, message: &str) {
        if let Err(something) = tx.send(Message::Close(Some(CloseFrame {
            code: close_code,
            reason: std::borrow::Cow::Borrowed(message),
        }))).await {
            error!("Failed to close WebSocket: {:?}", something);
        }
    }
}
