const languageButtons = document.querySelectorAll('[data-set-language]');
const translatedElements = document.querySelectorAll('[data-zh][data-en]');
let language = localStorage.getItem('vremoter-language') ||
  (navigator.language.toLowerCase().startsWith('zh') ? 'zh' : 'en');

function applyLanguage(nextLanguage) {
  language = nextLanguage;
  document.documentElement.lang = language === 'zh' ? 'zh-Hans' : 'en';
  translatedElements.forEach((element) => {
    element.textContent = element.dataset[language];
  });
  languageButtons.forEach((button) => {
    button.classList.toggle('active', button.dataset.setLanguage === language);
  });
  const audioImage = document.querySelector('[data-localized-audio]');
  if (audioImage) {
    audioImage.src = language === 'zh'
      ? 'assets/screenshots/vremoter-audio-1.1.0.png'
      : 'assets/screenshots/vremoter-audio-1.1.0-en.png';
  }
  const mappingImage = document.querySelector('[data-localized-mapping]');
  if (mappingImage) {
    mappingImage.src = language === 'zh'
      ? 'assets/screenshots/vremoter-mapping-chromecast-1.1.0.png'
      : 'assets/screenshots/vremoter-mapping-chromecast-1.1.0-en.png';
  }
  localStorage.setItem('vremoter-language', language);
}

languageButtons.forEach((button) => {
  button.addEventListener('click', () => applyLanguage(button.dataset.setLanguage));
});

const donationAssets = {
  wechat: ['assets/donate/wechat.JPG', 'WeChat payment QR code'],
  alipay: ['assets/donate/alipay.JPG', 'Alipay payment QR code'],
  paypal: ['assets/donate/paypal.JPG', 'PayPal payment QR code']
};
document.querySelectorAll('[data-donation]').forEach((button) => {
  button.addEventListener('click', () => {
    document.querySelectorAll('[data-donation]').forEach((item) => item.classList.remove('active'));
    button.classList.add('active');
    const [source, alt] = donationAssets[button.dataset.donation];
    const image = document.querySelector('#donation-code');
    image.src = source;
    image.alt = alt;
  });
});

applyLanguage(language);

const actions = [
  ['none', '不执行'], ['copy', '复制'], ['paste', '粘贴'], ['ctrl+c', 'Ctrl+C'], ['ctrl+v', 'Ctrl+V'],
  ['left', '左'], ['right', '右'], ['up', '上'], ['down', '下'], ['enter', '确认']
];
const defaultKeymap = {
  'VK_BROWSER_HOME': { single: 'copy', double: 'paste' }, 'LEFT': { single: 'none', double: 'none' },
  'RIGHT': { single: 'none', double: 'none' }, 'UP/HOME': { single: 'none', double: 'none' },
  'DOWN': { single: 'none', double: 'none' }, 'ENTER/OK': { single: 'none', double: 'none' },
  'FUNCTION-C': { single: 'none', double: 'none' }, 'FUNCTION-D': { single: 'none', double: 'none' }
};
const keymap = JSON.parse(localStorage.getItem('vremoter-keymap') || 'null') || structuredClone(defaultKeymap);
const editor = document.querySelector('#keymap-editor');
const keyboardLabels = ['Esc','Q','W','E','R','T','Y','U','I','O','P','A','S','D','F','G','H','J','K','L','Z','X','C','V','B','N','M','←','→','Space','Fn','Enter'];
document.querySelector('#keyboard-grid').innerHTML = keyboardLabels.map((label, index) => `<button data-key="FUNCTION-${String.fromCharCode(65 + (index % 6))}">${label}</button>`).join('');
function renderKeymap() {
  editor.innerHTML = Object.entries(keymap).map(([key, values]) => `<div class="keymap-row" data-key="${key}"><strong>${key}</strong>${['single', 'double'].map((kind) => `<label>${kind === 'single' ? '单击' : '双击'}<select data-key="${key}" data-kind="${kind}">${actions.map(([value, label]) => `<option value="${value}" ${values[kind] === value ? 'selected' : ''}>${label}</option>`).join('')}</select></label>`).join('')}</div>`).join('');
  editor.querySelectorAll('select').forEach((select) => select.addEventListener('change', () => { keymap[select.dataset.key][select.dataset.kind] = select.value; localStorage.setItem('vremoter-keymap', JSON.stringify(keymap)); document.querySelector('#config-status').textContent = '已保存到浏览器'; }));
}
document.querySelector('#download-keymap').addEventListener('click', () => {
  const blob = new Blob([JSON.stringify({ keys: keymap }, null, 2)], { type: 'application/json' });
  const link = document.createElement('a'); link.href = URL.createObjectURL(blob); link.download = 'keymap.json'; link.click(); URL.revokeObjectURL(link.href);
  document.querySelector('#config-status').textContent = '配置文件已下载';
});
document.querySelector('#reset-keymap').addEventListener('click', () => { Object.assign(keymap, structuredClone(defaultKeymap)); localStorage.removeItem('vremoter-keymap'); renderKeymap(); document.querySelector('#config-status').textContent = '已恢复默认'; });
renderKeymap();
document.querySelectorAll('[data-remote-view]').forEach((button) => button.addEventListener('click', () => {
  const back = button.dataset.remoteView === 'back';
  document.querySelectorAll('[data-remote-view]').forEach((item) => item.classList.toggle('active', item === button));
  document.querySelector('#remote-front').classList.toggle('hidden', back);
  document.querySelector('#remote-back').classList.toggle('visible', back);
  document.querySelector('#remote-mode-label').textContent = back ? '背面键盘 · 32 个可映射键' : '正面遥控器 · 12 个可映射键';
}));
document.querySelectorAll('[data-key]').forEach((button) => button.addEventListener('click', () => {
  const key = button.dataset.key;
  const row = [...document.querySelectorAll('.keymap-row')].find((item) => item.dataset.key === key);
  if (row) editor.scrollTop = row.offsetTop - editor.offsetTop;
}));
