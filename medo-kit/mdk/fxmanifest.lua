fx_version 'cerulean'
game 'gta5'
lua54 'yes'

name 'mdk'
description 'MDK - medo kit: six drop-in scripts with one control panel'
version '0.1.0'

shared_script 'config.lua'

server_scripts {
  'server/security.lua',
  'server/bridge.lua',
  'server/core.lua',
  'server/m_zone.lua',
  'server/m_bag.lua',
  'server/m_police.lua',
  'server/m_press.lua',
  'server/m_market.lua',
  'server/m_party.lua',
}

client_scripts {
  'client/world.lua',
  'client/core.lua',
  'client/m_zone.lua',
  'client/m_bag.lua',
  'client/m_police.lua',
  'client/m_market.lua',
  'client/m_party.lua',
}

ui_page 'html/index.html'
files { 'html/index.html', 'html/style.css', 'html/app.js' }
