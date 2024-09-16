use crate::chen_config::ChenConfig;
use crate::worker::Worker;

mod chen_config;
mod worker;
mod models;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    env_logger::init();
    
    let config: ChenConfig = ChenConfig::load("config.json");

    let mut worker = Worker::new(config);

    worker.start().await;

    Ok(())
}
