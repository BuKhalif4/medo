fx_version 'cerulean'
game 'gta5'
lua54 'yes'

name 'medo'
description 'MEDO - crime and police sandbox'
version '0.1.0'

shared_script 'config.lua'

server_scripts {
  'server/security.lua',
  'server/bridge.lua',
  'server/main.lua',
  'server/market.lua',
  'server/police.lua',
  'server/heist.lua',
  'server/party.lua',
  'server/vehicles.lua',
}

client_scripts {
  'client/world.lua',
  'client/main.lua',
}

ui_page 'html/index.html'
files { 'html/index.html', 'html/style.css', 'html/app.js' }
