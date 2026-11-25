fx_version 'cerulean'
game 'gta5'
lua54 'yes'

name 'bagcity'
description 'Bag City - loot bags that remember every hand they passed through'
version '0.5.0'

shared_script 'config.lua'

server_scripts {
  'server/security.lua',
  'server/bridge.lua',
  'server/main.lua',
  'server/market.lua',
}

client_scripts {
  'client/world.lua',
  'client/main.lua',
}

ui_page 'html/index.html'

files {
  'html/index.html',
  'html/style.css',
  'html/app.js',
}
