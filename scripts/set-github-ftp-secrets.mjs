import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import sodium from 'libsodium-wrappers'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const root = path.join(__dirname, '..')
const envPath = path.join(root, '.deploy', 'ftp-config.env')

function parseEnv(filePath) {
  const map = {}
  for (const line of fs.readFileSync(filePath, 'utf8').split(/\r?\n/)) {
    if (!line || line.trim().startsWith('#') || !line.includes('=')) continue
    const i = line.indexOf('=')
    map[line.slice(0, i).trim()] = line.slice(i + 1).trim()
  }
  return map
}

async function encryptSecret(publicKeyBase64, secretValue) {
  await sodium.ready
  const key = sodium.from_base64(publicKeyBase64, sodium.base64_variants.ORIGINAL)
  const message = sodium.from_string(secretValue)
  const encrypted = sodium.crypto_box_seal(message, key)
  return sodium.to_base64(encrypted, sodium.base64_variants.ORIGINAL)
}

async function putSecret(token, name, value) {
  const repo = 'usamaghazi367/Steelstone-ERP'
  const keyRes = await fetch(`https://api.github.com/repos/${repo}/actions/secrets/public-key`, {
    headers: { Authorization: `Bearer ${token}`, 'User-Agent': 'ConstFire-Setup' },
  })
  if (!keyRes.ok) throw new Error(`public-key ${keyRes.status}`)
  const { key, key_id } = await keyRes.json()
  const encrypted = await encryptSecret(key, value)
  const res = await fetch(`https://api.github.com/repos/${repo}/actions/secrets/${name}`, {
    method: 'PUT',
    headers: {
      Authorization: `Bearer ${token}`,
      'User-Agent': 'ConstFire-Setup',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ encrypted_value: encrypted, key_id }),
  })
  if (!res.ok) throw new Error(`${name} ${res.status} ${await res.text()}`)
}

const token = process.env.GITHUB_TOKEN
if (!token) {
  console.error('GITHUB_TOKEN missing')
  process.exit(1)
}
if (!fs.existsSync(envPath)) {
  console.error('Missing ftp-config.env')
  process.exit(1)
}

const cfg = parseEnv(envPath)
const pairs = [
  ['SMARTERASP_FTP_SERVER', cfg.SMARTERASP_FTP_SERVER],
  ['SMARTERASP_FTP_USERNAME', cfg.SMARTERASP_FTP_USERNAME],
  ['SMARTERASP_FTP_PASSWORD', cfg.SMARTERASP_FTP_PASSWORD],
  ['SMARTERASP_FTP_REMOTE_DIR', cfg.SMARTERASP_FTP_REMOTE_DIR || '/'],
]

for (const [name, value] of pairs) {
  if (!value || value.includes('PUT_FTP')) throw new Error(`Invalid ${name}`)
  await putSecret(token, name, value)
  console.log(`Set secret ${name}`)
}
