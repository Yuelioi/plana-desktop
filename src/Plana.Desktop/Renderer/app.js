const element = document.querySelector('#player')
let player = null
let idle = ''
let animations = []
let pointer = null
let clickTimer = 0
const native = new URLSearchParams(location.search).has('native')
if (native) document.body.classList.add('native')

function post(message) { window.chrome?.webview?.postMessage(message) }
function randomAnimation() {
  const preferred = animations.filter(name => name !== idle && /pat|look|touch|tap|talk|^s_/i.test(name))
  const fallback = animations.filter(name => name !== idle && !/setup|setting|left|right/i.test(name))
  const candidates = preferred.length ? preferred : fallback
  return candidates[Math.floor(Math.random() * candidates.length)] || idle
}
function playAnimation(name) {
  if (!player) return
  const selected = name === 'random' ? randomAnimation() : name
  if (!selected) return
  player.animationState.setAnimation(0, selected, false)
  if (idle) player.animationState.addAnimation(0, idle, true, 0)
}
function configureToolbar(config) {
  if (!native) return
  document.documentElement.lang = config?.isChinese ? 'zh-CN' : 'en'
  const groupSelect = document.querySelector('#quick-group')
  const actionSelect = document.querySelector('#quick-action')
  const groups = config?.groups || []
  const actions = config?.actions || []
  const prompt = document.querySelector('#ai-prompt')
  const settingsButton = document.querySelector('#quick-settings')
  const collapseButton = document.querySelector('#quick-collapse')
  const runButton = document.querySelector('#quick-run')
  prompt.placeholder = config?.chatPlaceholder || 'Say something…'
  prompt.setAttribute('aria-label', prompt.placeholder)
  settingsButton.title = config?.settingsLabel || 'Settings'
  settingsButton.setAttribute('aria-label', settingsButton.title)
  collapseButton.title = config?.collapseLabel || 'Collapse tools'
  collapseButton.setAttribute('aria-label', collapseButton.title)
  runButton.title = config?.runLabel || 'Run'
  runButton.setAttribute('aria-label', runButton.title)
  groupSelect.replaceChildren(new Option(config?.groupPlaceholder || 'Quick tools', ''))
  for (const group of groups) groupSelect.add(new Option(group.name, group.id))
  function refreshActions() {
    const group = groups.find(item => item.id === groupSelect.value)
    const allowed = group ? new Set(group.actionIds) : new Set()
    actionSelect.replaceChildren(new Option(config?.actionPlaceholder || 'Choose an action', ''))
    for (const action of actions.filter(item => allowed.has(item.id))) actionSelect.add(new Option(action.name, action.id))
    if (actionSelect.options.length > 1) actionSelect.selectedIndex = 1
  }
  groupSelect.onchange = () => {
    refreshActions()
    post({ type: 'toolbarGroupChanged', groupId: groupSelect.value })
  }
  if (config?.selectedGroupId && groups.some(group => group.id === config.selectedGroupId)) groupSelect.value = config.selectedGroupId
  refreshActions()
}
function showAiResponse(text, isError) {
  const response = document.querySelector('#ai-response')
  const prompt = document.querySelector('#ai-prompt')
  response.textContent = text || ''
  response.hidden = !text
  response.classList.toggle('error', Boolean(isError))
  prompt.disabled = false
  prompt.placeholder = prompt.dataset.placeholder || prompt.placeholder
  prompt.focus()
}
window.plana = { playAnimation, configureToolbar, showAiResponse }

if (native) {
  const prompt = document.querySelector('#ai-prompt')
  prompt.addEventListener('keydown', event => {
    if (event.key !== 'Enter' || !prompt.value.trim()) return
    event.preventDefault()
    const value = prompt.value.trim()
    prompt.value = ''
    prompt.dataset.placeholder = prompt.placeholder
    prompt.placeholder = document.documentElement.lang === 'zh-CN' ? '正在思考…' : 'Thinking…'
    prompt.disabled = true
    post({ type: 'aiPrompt', prompt: value })
  })
  document.querySelector('#quick-settings').addEventListener('click', () => post({ type: 'toolbarSettings' }))
  document.querySelector('#quick-collapse').addEventListener('click', () => {
    document.body.classList.toggle('tools-collapsed')
    const collapsed = document.body.classList.contains('tools-collapsed')
    document.querySelector('#quick-collapse').textContent = collapsed ? '\uE70D' : '\uE70E'
    post({ type: 'toolbarCollapsed', collapsed })
  })
  document.querySelector('#quick-run').addEventListener('click', () => {
    const select = document.querySelector('#quick-action')
    post({ type: 'toolbarRun', actionId: select.value, label: select.selectedOptions[0]?.text || '' })
  })
}

element.addEventListener('pointerdown', event => {
  if (event.button !== 0) return
  pointer = { x: event.clientX, y: event.clientY }
  element.classList.add('dragging')
})
element.addEventListener('pointermove', event => {
  if (!pointer || Math.hypot(event.clientX - pointer.x, event.clientY - pointer.y) < 6) return
  pointer = null
  post({ type: 'drag' })
})
element.addEventListener('pointerup', () => {
  element.classList.remove('dragging')
  if (!pointer) return
  pointer = null
  window.clearTimeout(clickTimer)
  clickTimer = window.setTimeout(() => post({ type: 'interaction', interaction: 'click' }), 220)
})
element.addEventListener('dblclick', () => {
  window.clearTimeout(clickTimer)
  post({ type: 'interaction', interaction: 'doubleClick' })
})
element.addEventListener('contextmenu', event => {
  event.preventDefault()
  post({ type: 'context' })
})
element.addEventListener('pointercancel', () => { pointer = null; element.classList.remove('dragging') })

player = new spine.SpinePlayer('player', {
  skelUrl: '/spine/plana/NP0035_spr.skel',
  atlasUrl: '/spine/plana/NP0035_spr.atlas',
  showControls: false,
  premultipliedAlpha: false,
  backgroundColor: '#00000000',
  alpha: true,
  viewport: { autoSize: true, padLeft: '1%', padRight: '1%', padTop: '1%', padBottom: '1%' },
  success(instance) {
    player = instance
    animations = instance.skeleton?.data?.animations?.map(animation => animation.name) || []
    idle = animations.find(name => name === 'Idle_01') || animations.find(name => /idle/i.test(name)) || animations[0] || ''
    if (idle) instance.animationState.setAnimation(0, idle, true)
    post({ type: 'ready', animations })
  },
})
