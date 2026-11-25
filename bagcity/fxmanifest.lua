fx_version 'cerulean'
game 'gta5'
lua54 'yes'

name 'bagcity'
description 'Bag City - loot bags that remember every hand they passed through'
version '0.2.0'

shared_script 'config.lua'

server_scripts {
  'server/security.lua',
  'server/bridge.lua',
  'server/main.lua',
}

client_script 'client/main.lua'
