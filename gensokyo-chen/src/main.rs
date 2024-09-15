use crate::chen_config::ChenConfig;

mod chen_config;
mod worker;

fn main() {
    env_logger::init();
    
    let config: ChenConfig = ChenConfig::load("config.json");
}
