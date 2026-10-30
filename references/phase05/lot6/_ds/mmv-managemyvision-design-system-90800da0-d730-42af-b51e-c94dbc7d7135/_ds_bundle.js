/* @ds-bundle: {"format":4,"namespace":"MMVManageMyVisionDesignSystem_90800d","components":[{"name":"Button","sourcePath":"components/buttons/Button.jsx"},{"name":"IconButton","sourcePath":"components/buttons/IconButton.jsx"},{"name":"Card","sourcePath":"components/data/Card.jsx"},{"name":"ListItem","sourcePath":"components/data/ListItem.jsx"},{"name":"StatCard","sourcePath":"components/data/StatCard.jsx"},{"name":"Table","sourcePath":"components/data/Table.jsx"},{"name":"Alert","sourcePath":"components/feedback/Alert.jsx"},{"name":"Badge","sourcePath":"components/feedback/Badge.jsx"},{"name":"EmptyState","sourcePath":"components/feedback/EmptyState.jsx"},{"name":"Spinner","sourcePath":"components/feedback/Spinner.jsx"},{"name":"StatusDot","sourcePath":"components/feedback/StatusDot.jsx"},{"name":"Tag","sourcePath":"components/feedback/Tag.jsx"},{"name":"Toast","sourcePath":"components/feedback/Toast.jsx"},{"name":"Checkbox","sourcePath":"components/forms/Checkbox.jsx"},{"name":"DateField","sourcePath":"components/forms/DateField.jsx"},{"name":"Input","sourcePath":"components/forms/Input.jsx"},{"name":"Radio","sourcePath":"components/forms/Radio.jsx"},{"name":"SearchBox","sourcePath":"components/forms/SearchBox.jsx"},{"name":"Select","sourcePath":"components/forms/Select.jsx"},{"name":"Switch","sourcePath":"components/forms/Switch.jsx"},{"name":"Textarea","sourcePath":"components/forms/Textarea.jsx"},{"name":"FieldShell","sourcePath":"components/forms/fieldParts.jsx"},{"name":"Breadcrumb","sourcePath":"components/navigation/Breadcrumb.jsx"},{"name":"Modal","sourcePath":"components/navigation/Modal.jsx"},{"name":"NavItem","sourcePath":"components/navigation/NavItem.jsx"},{"name":"Tabs","sourcePath":"components/navigation/Tabs.jsx"}],"sourceHashes":{"components/buttons/Button.jsx":"7a06eedffc12","components/buttons/IconButton.jsx":"b9c3b4365b4d","components/data/Card.jsx":"8e61144dd54b","components/data/ListItem.jsx":"7fd8e86f1d38","components/data/StatCard.jsx":"7828ff1b8c44","components/data/Table.jsx":"4e925934e096","components/feedback/Alert.jsx":"2c9213c88984","components/feedback/Badge.jsx":"987430973d84","components/feedback/EmptyState.jsx":"c299ce7cf6c4","components/feedback/Spinner.jsx":"205e86bd69c3","components/feedback/StatusDot.jsx":"abb5a7487696","components/feedback/Tag.jsx":"3549b2164852","components/feedback/Toast.jsx":"10262ab99f69","components/forms/Checkbox.jsx":"5a6fdb0398f2","components/forms/DateField.jsx":"db8b5031588c","components/forms/Input.jsx":"47ff14d1041c","components/forms/Radio.jsx":"f24cd9c9b2e5","components/forms/SearchBox.jsx":"4978c31cb067","components/forms/Select.jsx":"94f79073ce3b","components/forms/Switch.jsx":"1bbc6ddfe222","components/forms/Textarea.jsx":"b51ece73e1a4","components/forms/fieldParts.jsx":"6978e5c62f0d","components/navigation/Breadcrumb.jsx":"3b9a28db6a8e","components/navigation/Modal.jsx":"39a39dd8d717","components/navigation/NavItem.jsx":"4b33da10ac52","components/navigation/Tabs.jsx":"0a42d9737591","ui_kits/mmv-desktop/app.jsx":"a922d18e945d","ui_kits/mmv-desktop/customerDetail.jsx":"98ca2b010455","ui_kits/mmv-desktop/customers.jsx":"81eb6135743b","ui_kits/mmv-desktop/dashboard.jsx":"79f68ae5f86d","ui_kits/mmv-desktop/data.js":"0a728778b61b","ui_kits/mmv-desktop/kanban.jsx":"8b832914b3b1","ui_kits/mmv-desktop/login.jsx":"f2226550a4dc","ui_kits/mmv-desktop/shell.jsx":"00c4136d4650"},"inlinedExternals":[],"unexposedExports":[{"name":"injectFieldCSS","sourcePath":"components/forms/fieldParts.jsx"}]} */

(() => {

const __ds_ns = (window.MMVManageMyVisionDesignSystem_90800d = window.MMVManageMyVisionDesignSystem_90800d || {});

const __ds_scope = {};

(__ds_ns.__errors = __ds_ns.__errors || []);

// components/buttons/Button.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-btn{
  font-family:var(--font-body);font-weight:var(--weight-semibold);
  display:inline-flex;align-items:center;justify-content:center;gap:8px;
  border:1px solid transparent;border-radius:var(--radius-sm);
  cursor:pointer;white-space:nowrap;line-height:1;
  transition:background .13s ease,border-color .13s ease,color .13s ease,box-shadow .13s ease,transform .05s ease;
  -webkit-user-select:none;user-select:none;
}
.mmv-btn:focus-visible{outline:none;box-shadow:var(--shadow-focus)}
.mmv-btn:active{transform:translateY(0.5px)}
.mmv-btn--sm{padding:6px 12px;font-size:var(--text-sm)}
.mmv-btn--md{padding:10px 18px;font-size:var(--text-body)}
.mmv-btn--lg{padding:13px 22px;font-size:var(--text-body-lg)}
.mmv-btn--full{width:100%}
.mmv-btn__ic{display:inline-flex;flex:0 0 auto}
.mmv-btn__ic svg{display:block}

/* primary */
.mmv-btn--primary{background:var(--accent);color:var(--text-on-accent)}
.mmv-btn--primary:hover{background:var(--accent-hover)}
.mmv-btn--primary:active{background:var(--accent-pressed)}

/* secondary (outlined) */
.mmv-btn--secondary{background:var(--surface-card);color:var(--accent);border-color:var(--accent)}
.mmv-btn--secondary:hover{background:var(--accent-soft)}
.mmv-btn--secondary:active{background:var(--accent-soft);border-color:var(--accent-pressed);color:var(--accent-pressed)}

/* tertiary (text) */
.mmv-btn--tertiary{background:transparent;color:var(--accent)}
.mmv-btn--tertiary:hover{background:var(--accent-soft)}
.mmv-btn--tertiary:active{background:var(--accent-soft);color:var(--accent-pressed)}

/* destructive */
.mmv-btn--destructive{background:var(--error);color:#fff}
.mmv-btn--destructive:hover{filter:brightness(1.06)}
.mmv-btn--destructive:active{filter:brightness(0.92)}

.mmv-btn:disabled,.mmv-btn[aria-disabled="true"]{
  background:var(--surface-sunken);color:var(--text-disabled);
  border-color:transparent;cursor:not-allowed;filter:none;transform:none;box-shadow:none;
}
.mmv-btn--tertiary:disabled,.mmv-btn--tertiary[aria-disabled="true"]{background:transparent}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-button-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-button-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
function Button({
  variant = 'primary',
  size = 'md',
  leftIcon,
  rightIcon,
  fullWidth = false,
  disabled = false,
  type = 'button',
  className = '',
  children,
  ...rest
}) {
  const cls = ['mmv-btn', `mmv-btn--${variant}`, `mmv-btn--${size}`, fullWidth && 'mmv-btn--full', className].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("button", _extends({
    type: type,
    className: cls,
    disabled: disabled
  }, rest), leftIcon && /*#__PURE__*/React.createElement("span", {
    className: "mmv-btn__ic"
  }, leftIcon), children != null && /*#__PURE__*/React.createElement("span", null, children), rightIcon && /*#__PURE__*/React.createElement("span", {
    className: "mmv-btn__ic"
  }, rightIcon));
}
Object.assign(__ds_scope, { Button });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/buttons/Button.jsx", error: String((e && e.message) || e) }); }

// components/buttons/IconButton.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-iconbtn{
  display:inline-flex;align-items:center;justify-content:center;
  border:1px solid transparent;border-radius:var(--radius-sm);cursor:pointer;
  transition:background .13s ease,border-color .13s ease,color .13s ease,box-shadow .13s ease;
  padding:0;line-height:0;
}
.mmv-iconbtn:focus-visible{outline:none;box-shadow:var(--shadow-focus)}
.mmv-iconbtn svg{display:block}
.mmv-iconbtn--sm{width:32px;height:32px}
.mmv-iconbtn--md{width:40px;height:40px}
.mmv-iconbtn--lg{width:48px;height:48px}

/* primary — solid accent */
.mmv-iconbtn--primary{background:var(--accent);color:#fff}
.mmv-iconbtn--primary:hover{background:var(--accent-hover)}
.mmv-iconbtn--primary:active{background:var(--accent-pressed)}

/* secondary — outlined accent */
.mmv-iconbtn--secondary{background:var(--surface-card);color:var(--accent);border-color:var(--accent)}
.mmv-iconbtn--secondary:hover{background:var(--accent-soft)}

/* ghost — muted, for toolbars / table row actions */
.mmv-iconbtn--ghost{background:transparent;color:var(--text-secondary)}
.mmv-iconbtn--ghost:hover{background:var(--surface-hover);color:var(--text-primary)}
.mmv-iconbtn--ghost:active{background:var(--surface-selected)}

.mmv-iconbtn:disabled,.mmv-iconbtn[aria-disabled="true"]{
  background:var(--surface-sunken);color:var(--text-disabled);border-color:transparent;cursor:not-allowed;
}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-iconbtn-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-iconbtn-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
function IconButton({
  variant = 'ghost',
  size = 'md',
  'aria-label': ariaLabel,
  disabled = false,
  type = 'button',
  className = '',
  children,
  ...rest
}) {
  const cls = ['mmv-iconbtn', `mmv-iconbtn--${variant}`, `mmv-iconbtn--${size}`, className].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("button", _extends({
    type: type,
    className: cls,
    "aria-label": ariaLabel,
    disabled: disabled
  }, rest), children);
}
Object.assign(__ds_scope, { IconButton });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/buttons/IconButton.jsx", error: String((e && e.message) || e) }); }

// components/data/Card.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-card{background:var(--surface-card);border:1px solid var(--border);border-radius:var(--radius-md);font-family:var(--font-body);color:var(--text-primary)}
.mmv-card--e1{box-shadow:var(--shadow-1);border-color:transparent}
.mmv-card--e2{box-shadow:var(--shadow-2);border-color:transparent}
.mmv-card--feature{border-radius:var(--radius-xl)}
.mmv-card--pad{padding:20px}
.mmv-card--interactive{cursor:pointer;transition:box-shadow .14s,border-color .14s,transform .06s}
.mmv-card--interactive:hover{box-shadow:var(--shadow-2);border-color:var(--border-strong)}
.mmv-card--interactive:active{transform:translateY(.5px)}
.mmv-card__head{display:flex;align-items:flex-start;justify-content:space-between;gap:12px;padding:16px 20px;border-bottom:1px solid var(--border)}
.mmv-card__title{font-family:var(--font-heading);font-size:var(--text-h3);font-weight:var(--weight-semibold);margin:0;letter-spacing:-.01em}
.mmv-card__sub{font-size:var(--text-sm);color:var(--text-secondary);margin:2px 0 0}
.mmv-card__body{padding:20px}
.mmv-card__foot{padding:14px 20px;border-top:1px solid var(--border);display:flex;align-items:center;justify-content:flex-end;gap:10px}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-card-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-card-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
function Card({
  title,
  subtitle,
  headerActions,
  footer,
  elevation = 1,
  feature = false,
  interactive = false,
  padded,
  className = '',
  children,
  ...rest
}) {
  const hasHeader = title || subtitle || headerActions;
  const usePad = padded !== undefined ? padded : !hasHeader && !footer;
  const cls = ['mmv-card', elevation === 2 ? 'mmv-card--e2' : elevation === 1 ? 'mmv-card--e1' : '', feature && 'mmv-card--feature', interactive && 'mmv-card--interactive', usePad && 'mmv-card--pad', className].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("div", _extends({
    className: cls
  }, rest), hasHeader && /*#__PURE__*/React.createElement("div", {
    className: "mmv-card__head"
  }, /*#__PURE__*/React.createElement("div", null, title && /*#__PURE__*/React.createElement("h3", {
    className: "mmv-card__title"
  }, title), subtitle && /*#__PURE__*/React.createElement("p", {
    className: "mmv-card__sub"
  }, subtitle)), headerActions && /*#__PURE__*/React.createElement("div", null, headerActions)), hasHeader || footer ? /*#__PURE__*/React.createElement("div", {
    className: "mmv-card__body"
  }, children) : children, footer && /*#__PURE__*/React.createElement("div", {
    className: "mmv-card__foot"
  }, footer));
}
Object.assign(__ds_scope, { Card });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/data/Card.jsx", error: String((e && e.message) || e) }); }

// components/data/ListItem.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-listitem{display:flex;align-items:center;gap:14px;padding:12px 14px;font-family:var(--font-body);
  background:var(--surface-card);border:1px solid var(--border);border-radius:var(--radius-sm);
  text-align:left;width:100%;transition:background .12s,border-color .12s}
button.mmv-listitem{cursor:pointer;font:inherit;color:inherit}
.mmv-listitem--clickable:hover{background:var(--surface-hover);border-color:var(--border-strong)}
.mmv-listitem__lead{flex:0 0 auto;display:inline-flex;align-items:center;justify-content:center}
.mmv-listitem__avatar{width:40px;height:40px;border-radius:50%;background:var(--accent-soft);color:var(--accent);
  display:inline-flex;align-items:center;justify-content:center;font-weight:var(--weight-semibold);font-size:var(--text-sm)}
.mmv-listitem__icon{width:38px;height:38px;border-radius:var(--radius-sm);background:var(--surface-sunken);color:var(--text-secondary);
  display:inline-flex;align-items:center;justify-content:center}
.mmv-listitem__icon svg,.mmv-listitem__icon i{width:18px;height:18px}
.mmv-listitem__body{flex:1 1 auto;min-width:0}
.mmv-listitem__title{font-size:var(--text-body);font-weight:var(--weight-semibold);color:var(--text-primary);margin:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.mmv-listitem__desc{font-size:var(--text-xs);color:var(--text-secondary);margin:1px 0 0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.mmv-listitem__trail{flex:0 0 auto;display:inline-flex;align-items:center;gap:10px;color:var(--text-tertiary)}
.mmv-listitem__chev{display:inline-flex;color:var(--text-tertiary)}
.mmv-listitem__chev svg{width:16px;height:16px}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-listitem-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-listitem-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
const Chev = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "m9 18 6-6-6-6"
}));
function ListItem({
  avatar,
  icon,
  title,
  description,
  trailing,
  chevron = false,
  onClick,
  className = '',
  ...rest
}) {
  const clickable = !!onClick;
  const Tag = clickable ? 'button' : 'div';
  const lead = avatar ? /*#__PURE__*/React.createElement("span", {
    className: "mmv-listitem__avatar"
  }, avatar) : icon ? /*#__PURE__*/React.createElement("span", {
    className: "mmv-listitem__icon"
  }, icon) : null;
  return /*#__PURE__*/React.createElement(Tag, _extends({
    className: ['mmv-listitem', clickable && 'mmv-listitem--clickable', className].filter(Boolean).join(' '),
    onClick: onClick
  }, rest), lead && /*#__PURE__*/React.createElement("span", {
    className: "mmv-listitem__lead"
  }, lead), /*#__PURE__*/React.createElement("span", {
    className: "mmv-listitem__body"
  }, title && /*#__PURE__*/React.createElement("p", {
    className: "mmv-listitem__title"
  }, title), description && /*#__PURE__*/React.createElement("p", {
    className: "mmv-listitem__desc"
  }, description)), trailing && /*#__PURE__*/React.createElement("span", {
    className: "mmv-listitem__trail"
  }, trailing), chevron && /*#__PURE__*/React.createElement("span", {
    className: "mmv-listitem__chev"
  }, /*#__PURE__*/React.createElement(Chev, null)));
}
Object.assign(__ds_scope, { ListItem });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/data/ListItem.jsx", error: String((e && e.message) || e) }); }

// components/data/StatCard.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-stat{font-family:var(--font-body);border-radius:var(--radius-md);padding:18px 20px;position:relative;overflow:hidden}
.mmv-stat--default{background:var(--surface-card);border:1px solid var(--border)}
.mmv-stat--accent{background:linear-gradient(135deg,var(--accent) 0%,#0B57D6 100%);color:#fff}
.mmv-stat__top{display:flex;align-items:flex-start;justify-content:space-between;gap:12px}
.mmv-stat__ic{width:40px;height:40px;border-radius:var(--radius-sm);display:inline-flex;align-items:center;justify-content:center}
.mmv-stat--default .mmv-stat__ic{background:var(--accent-soft);color:var(--accent)}
.mmv-stat--accent .mmv-stat__ic{background:rgba(255,255,255,.18);color:#fff}
.mmv-stat__ic svg,.mmv-stat__ic i{width:20px;height:20px}
.mmv-stat__value{font-family:var(--font-heading);font-size:34px;font-weight:var(--weight-bold);letter-spacing:-.02em;line-height:1.05;margin-top:14px}
.mmv-stat__label{font-size:var(--text-sm);font-weight:var(--weight-medium);margin-top:2px}
.mmv-stat--default .mmv-stat__label{color:var(--text-secondary)}
.mmv-stat--accent .mmv-stat__label{color:rgba(255,255,255,.82)}
.mmv-stat__trend{display:inline-flex;align-items:center;gap:4px;font-size:var(--text-xs);font-weight:var(--weight-semibold);padding:4px 8px;border-radius:var(--radius-full)}
.mmv-stat__trend svg{width:11px;height:11px}
.mmv-stat--default .mmv-stat__trend.up{background:var(--success-soft);color:var(--success-text)}
.mmv-stat--default .mmv-stat__trend.down{background:var(--error-soft);color:var(--error-text)}
.mmv-stat--accent .mmv-stat__trend{background:rgba(255,255,255,.2);color:#fff}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-stat-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-stat-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
const Up = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "3",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "m5 15 7-7 7 7"
}));
const Down = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "3",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "m19 9-7 7-7-7"
}));
function StatCard({
  variant = 'default',
  icon,
  value,
  label,
  trend,
  trendDirection = 'up',
  className = '',
  children,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("div", _extends({
    className: ['mmv-stat', `mmv-stat--${variant}`, className].filter(Boolean).join(' ')
  }, rest), /*#__PURE__*/React.createElement("div", {
    className: "mmv-stat__top"
  }, icon && /*#__PURE__*/React.createElement("span", {
    className: "mmv-stat__ic"
  }, icon), trend && /*#__PURE__*/React.createElement("span", {
    className: ['mmv-stat__trend', trendDirection].join(' ')
  }, trendDirection === 'down' ? /*#__PURE__*/React.createElement(Down, null) : /*#__PURE__*/React.createElement(Up, null), trend)), /*#__PURE__*/React.createElement("div", {
    className: "mmv-stat__value"
  }, value), /*#__PURE__*/React.createElement("div", {
    className: "mmv-stat__label"
  }, label), children);
}
Object.assign(__ds_scope, { StatCard });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/data/StatCard.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Alert.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-alert{display:flex;gap:12px;align-items:flex-start;font-family:var(--font-body);
  padding:12px 14px;border-radius:var(--radius-sm);border:1px solid transparent;font-size:var(--text-sm);line-height:var(--leading-snug)}
.mmv-alert__ic{flex:0 0 auto;display:inline-flex;margin-top:1px}
.mmv-alert__ic svg{width:18px;height:18px}
.mmv-alert__body{flex:1 1 auto;min-width:0}
.mmv-alert__title{font-weight:var(--weight-semibold);margin-bottom:2px;color:var(--text-primary)}
.mmv-alert__x{flex:0 0 auto;border:0;background:transparent;cursor:pointer;color:var(--text-tertiary);padding:2px;border-radius:var(--radius-xs);display:inline-flex}
.mmv-alert__x:hover{color:var(--text-primary);background:rgba(0,0,0,.05)}
.mmv-alert__x svg{width:15px;height:15px}
.mmv-alert--info{background:var(--info-soft);border-color:color-mix(in srgb,var(--info) 30%,transparent)}
.mmv-alert--info .mmv-alert__ic{color:var(--info)}
.mmv-alert--success{background:var(--success-soft);border-color:color-mix(in srgb,var(--success) 30%,transparent)}
.mmv-alert--success .mmv-alert__ic{color:var(--success)}
.mmv-alert--warning{background:var(--warning-soft);border-color:color-mix(in srgb,var(--warning) 30%,transparent)}
.mmv-alert--warning .mmv-alert__ic{color:var(--warning)}
.mmv-alert--error{background:var(--error-soft);border-color:color-mix(in srgb,var(--error) 30%,transparent)}
.mmv-alert--error .mmv-alert__ic{color:var(--error)}
.mmv-alert__body,.mmv-alert__msg{color:var(--text-secondary)}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-alert-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-alert-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
const ICONS = {
  info: /*#__PURE__*/React.createElement("svg", {
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: "2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("circle", {
    cx: "12",
    cy: "12",
    r: "10"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M12 16v-4"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M12 8h.01"
  })),
  success: /*#__PURE__*/React.createElement("svg", {
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: "2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("path", {
    d: "M21.801 10A10 10 0 1 1 17 3.335"
  }), /*#__PURE__*/React.createElement("path", {
    d: "m9 11 3 3L22 4"
  })),
  warning: /*#__PURE__*/React.createElement("svg", {
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: "2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("path", {
    d: "m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M12 9v4"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M12 17h.01"
  })),
  error: /*#__PURE__*/React.createElement("svg", {
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: "2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("circle", {
    cx: "12",
    cy: "12",
    r: "10"
  }), /*#__PURE__*/React.createElement("path", {
    d: "m15 9-6 6"
  }), /*#__PURE__*/React.createElement("path", {
    d: "m9 9 6 6"
  }))
};
const X = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "M18 6 6 18"
}), /*#__PURE__*/React.createElement("path", {
  d: "m6 6 12 12"
}));
function Alert({
  tone = 'info',
  title,
  onClose,
  className = '',
  children,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("div", _extends({
    className: ['mmv-alert', `mmv-alert--${tone}`, className].filter(Boolean).join(' '),
    role: "alert"
  }, rest), /*#__PURE__*/React.createElement("span", {
    className: "mmv-alert__ic"
  }, ICONS[tone]), /*#__PURE__*/React.createElement("div", {
    className: "mmv-alert__body"
  }, title && /*#__PURE__*/React.createElement("div", {
    className: "mmv-alert__title"
  }, title), children && /*#__PURE__*/React.createElement("div", {
    className: "mmv-alert__msg"
  }, children)), onClose && /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: "mmv-alert__x",
    "aria-label": "Fermer",
    onClick: onClose
  }, /*#__PURE__*/React.createElement(X, null)));
}
Object.assign(__ds_scope, { Alert });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Alert.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Badge.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-badge{display:inline-flex;align-items:center;gap:6px;font-family:var(--font-body);
  font-size:var(--text-xs);font-weight:var(--weight-semibold);line-height:1;
  padding:4px 10px;border-radius:var(--radius-full);white-space:nowrap;border:1px solid transparent}
.mmv-badge__dot{width:7px;height:7px;border-radius:50%;background:currentColor;flex:0 0 auto}
.mmv-badge--success{background:var(--success-soft);color:var(--success-text)}
.mmv-badge--warning{background:var(--warning-soft);color:var(--warning-text)}
.mmv-badge--error{background:var(--error-soft);color:var(--error-text)}
.mmv-badge--info{background:var(--info-soft);color:var(--info-text)}
.mmv-badge--accent{background:var(--accent-soft);color:var(--accent)}
.mmv-badge--teal{background:rgba(0,194,168,.14);color:#087f6e}
.mmv-badge--neutral{background:var(--surface-sunken);color:var(--text-secondary)}
:root[data-theme="dark"] .mmv-badge--teal,.theme-dark .mmv-badge--teal{color:#3ee0c8}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-badge-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-badge-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
function Badge({
  tone = 'neutral',
  dot = false,
  className = '',
  children,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("span", _extends({
    className: ['mmv-badge', `mmv-badge--${tone}`, className].filter(Boolean).join(' ')
  }, rest), dot && /*#__PURE__*/React.createElement("span", {
    className: "mmv-badge__dot"
  }), children);
}
Object.assign(__ds_scope, { Badge });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Badge.jsx", error: String((e && e.message) || e) }); }

// components/feedback/EmptyState.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-empty{display:flex;flex-direction:column;align-items:center;text-align:center;
  font-family:var(--font-body);padding:40px 24px;max-width:420px;margin:0 auto}
.mmv-empty__ic{width:56px;height:56px;border-radius:var(--radius-lg);background:var(--accent-soft);
  color:var(--accent);display:inline-flex;align-items:center;justify-content:center;margin-bottom:16px}
.mmv-empty__ic svg,.mmv-empty__ic i{width:26px;height:26px}
.mmv-empty__title{font-family:var(--font-heading);font-size:var(--text-h3);font-weight:var(--weight-semibold);color:var(--text-primary);margin:0 0 6px}
.mmv-empty__desc{font-size:var(--text-sm);color:var(--text-secondary);line-height:var(--leading-normal);margin:0 0 18px}
.mmv-empty__actions{display:flex;gap:10px;flex-wrap:wrap;justify-content:center}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-empty-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-empty-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
function EmptyState({
  icon,
  title,
  description,
  actions,
  className = '',
  ...rest
}) {
  return /*#__PURE__*/React.createElement("div", _extends({
    className: ['mmv-empty', className].filter(Boolean).join(' ')
  }, rest), icon && /*#__PURE__*/React.createElement("span", {
    className: "mmv-empty__ic"
  }, icon), title && /*#__PURE__*/React.createElement("h3", {
    className: "mmv-empty__title"
  }, title), description && /*#__PURE__*/React.createElement("p", {
    className: "mmv-empty__desc"
  }, description), actions && /*#__PURE__*/React.createElement("div", {
    className: "mmv-empty__actions"
  }, actions));
}
Object.assign(__ds_scope, { EmptyState });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/EmptyState.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Spinner.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-spinner{display:inline-block;border-radius:50%;border-style:solid;
  border-color:var(--accent-soft);border-top-color:var(--accent);animation:mmv-spin .7s linear infinite}
.mmv-spinner--sm{width:16px;height:16px;border-width:2px}
.mmv-spinner--md{width:24px;height:24px;border-width:3px}
.mmv-spinner--lg{width:40px;height:40px;border-width:4px}
@keyframes mmv-spin{to{transform:rotate(360deg)}}
.mmv-spinbox{display:inline-flex;flex-direction:column;align-items:center;gap:12px;font-family:var(--font-body);color:var(--text-secondary);font-size:var(--text-sm)}
@media (prefers-reduced-motion:reduce){.mmv-spinner{animation-duration:1.6s}}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-spinner-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-spinner-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
function Spinner({
  size = 'md',
  label,
  className = '',
  ...rest
}) {
  const spin = /*#__PURE__*/React.createElement("span", _extends({
    className: ['mmv-spinner', `mmv-spinner--${size}`, className].filter(Boolean).join(' '),
    role: "status",
    "aria-label": label || 'Chargement'
  }, rest));
  if (!label) return spin;
  return /*#__PURE__*/React.createElement("span", {
    className: "mmv-spinbox"
  }, spin, /*#__PURE__*/React.createElement("span", null, label));
}
Object.assign(__ds_scope, { Spinner });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Spinner.jsx", error: String((e && e.message) || e) }); }

// components/feedback/StatusDot.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-statusdot{display:inline-flex;align-items:center;gap:8px;font-family:var(--font-body);font-size:var(--text-sm);color:var(--text-primary)}
.mmv-statusdot__d{width:8px;height:8px;border-radius:50%;flex:0 0 auto;position:relative}
.mmv-statusdot--success .mmv-statusdot__d{background:var(--success)}
.mmv-statusdot--warning .mmv-statusdot__d{background:var(--warning)}
.mmv-statusdot--error .mmv-statusdot__d{background:var(--error)}
.mmv-statusdot--info .mmv-statusdot__d{background:var(--info)}
.mmv-statusdot--neutral .mmv-statusdot__d{background:var(--text-tertiary)}
.mmv-statusdot--teal .mmv-statusdot__d{background:var(--brand-teal)}
.mmv-statusdot--pulse .mmv-statusdot__d::after{content:"";position:absolute;inset:-4px;border-radius:50%;background:inherit;opacity:.35;animation:mmv-pulse 1.6s ease-out infinite}
@keyframes mmv-pulse{0%{transform:scale(.6);opacity:.5}100%{transform:scale(1.6);opacity:0}}
@media (prefers-reduced-motion:reduce){.mmv-statusdot--pulse .mmv-statusdot__d::after{animation:none}}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-statusdot-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-statusdot-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
function StatusDot({
  tone = 'neutral',
  pulse = false,
  className = '',
  children,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("span", _extends({
    className: ['mmv-statusdot', `mmv-statusdot--${tone}`, pulse && 'mmv-statusdot--pulse', className].filter(Boolean).join(' ')
  }, rest), /*#__PURE__*/React.createElement("span", {
    className: "mmv-statusdot__d"
  }), children && /*#__PURE__*/React.createElement("span", null, children));
}
Object.assign(__ds_scope, { StatusDot });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/StatusDot.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Tag.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-tag{display:inline-flex;align-items:center;gap:6px;font-family:var(--font-body);
  font-size:var(--text-xs);font-weight:var(--weight-medium);line-height:1;
  padding:5px 10px;border-radius:var(--radius-xs);white-space:nowrap;
  background:var(--accent-soft);color:var(--accent);border:1px solid transparent}
.mmv-tag--outline{background:transparent;border-color:var(--border-strong);color:var(--text-secondary)}
.mmv-tag--teal{background:rgba(0,194,168,.12);color:#087f6e}
.mmv-tag--warning{background:var(--warning-soft);color:var(--warning-text)}
.mmv-tag__x{display:inline-flex;cursor:pointer;opacity:.65;margin-left:1px}
.mmv-tag__x:hover{opacity:1}
.mmv-tag__x svg{width:12px;height:12px}
:root[data-theme="dark"] .mmv-tag--teal,.theme-dark .mmv-tag--teal{color:#3ee0c8}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-tag-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-tag-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
const X = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2.4",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "M18 6 6 18"
}), /*#__PURE__*/React.createElement("path", {
  d: "m6 6 12 12"
}));
function Tag({
  variant = 'soft',
  onRemove,
  className = '',
  children,
  ...rest
}) {
  const mod = variant === 'outline' ? 'mmv-tag--outline' : variant === 'teal' ? 'mmv-tag--teal' : variant === 'warning' ? 'mmv-tag--warning' : '';
  return /*#__PURE__*/React.createElement("span", _extends({
    className: ['mmv-tag', mod, className].filter(Boolean).join(' ')
  }, rest), children, onRemove && /*#__PURE__*/React.createElement("span", {
    className: "mmv-tag__x",
    role: "button",
    "aria-label": "Retirer",
    onClick: onRemove
  }, /*#__PURE__*/React.createElement(X, null)));
}
Object.assign(__ds_scope, { Tag });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Tag.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Toast.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-toast{display:flex;gap:12px;align-items:center;font-family:var(--font-body);
  background:var(--surface-raised);border:1px solid var(--border);box-shadow:var(--shadow-3);
  padding:12px 14px;border-radius:var(--radius-md);font-size:var(--text-sm);min-width:280px;max-width:420px}
.mmv-toast__ic{flex:0 0 auto;width:28px;height:28px;border-radius:var(--radius-sm);display:inline-flex;align-items:center;justify-content:center}
.mmv-toast__ic svg{width:16px;height:16px}
.mmv-toast__msg{flex:1 1 auto;min-width:0;color:var(--text-primary);font-weight:var(--weight-medium)}
.mmv-toast__x{flex:0 0 auto;border:0;background:transparent;cursor:pointer;color:var(--text-tertiary);display:inline-flex;padding:2px;border-radius:var(--radius-xs)}
.mmv-toast__x:hover{color:var(--text-primary)}
.mmv-toast__x svg{width:15px;height:15px}
.mmv-toast--success .mmv-toast__ic{background:var(--success-soft);color:var(--success)}
.mmv-toast--error .mmv-toast__ic{background:var(--error-soft);color:var(--error)}
.mmv-toast--warning .mmv-toast__ic{background:var(--warning-soft);color:var(--warning)}
.mmv-toast--info .mmv-toast__ic{background:var(--info-soft);color:var(--info)}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-toast-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-toast-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
const ICONS = {
  success: /*#__PURE__*/React.createElement("svg", {
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: "2.2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("path", {
    d: "M21.801 10A10 10 0 1 1 17 3.335"
  }), /*#__PURE__*/React.createElement("path", {
    d: "m9 11 3 3L22 4"
  })),
  error: /*#__PURE__*/React.createElement("svg", {
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: "2.2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("circle", {
    cx: "12",
    cy: "12",
    r: "10"
  }), /*#__PURE__*/React.createElement("path", {
    d: "m15 9-6 6"
  }), /*#__PURE__*/React.createElement("path", {
    d: "m9 9 6 6"
  })),
  warning: /*#__PURE__*/React.createElement("svg", {
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: "2.2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("path", {
    d: "m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M12 9v4"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M12 17h.01"
  })),
  info: /*#__PURE__*/React.createElement("svg", {
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: "2.2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("circle", {
    cx: "12",
    cy: "12",
    r: "10"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M12 16v-4"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M12 8h.01"
  }))
};
const X = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "M18 6 6 18"
}), /*#__PURE__*/React.createElement("path", {
  d: "m6 6 12 12"
}));
function Toast({
  tone = 'success',
  onClose,
  className = '',
  children,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("div", _extends({
    className: ['mmv-toast', `mmv-toast--${tone}`, className].filter(Boolean).join(' '),
    role: "status"
  }, rest), /*#__PURE__*/React.createElement("span", {
    className: "mmv-toast__ic"
  }, ICONS[tone]), /*#__PURE__*/React.createElement("span", {
    className: "mmv-toast__msg"
  }, children), onClose && /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: "mmv-toast__x",
    "aria-label": "Fermer",
    onClick: onClose
  }, /*#__PURE__*/React.createElement(X, null)));
}
Object.assign(__ds_scope, { Toast });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Toast.jsx", error: String((e && e.message) || e) }); }

// components/forms/Checkbox.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-check{display:inline-flex;align-items:flex-start;gap:10px;cursor:pointer;font-family:var(--font-body);font-size:var(--text-body);color:var(--text-primary);line-height:1.3}
.mmv-check--disabled{cursor:not-allowed;color:var(--text-disabled)}
.mmv-check input{position:absolute;opacity:0;width:0;height:0}
.mmv-check__box{flex:0 0 auto;width:18px;height:18px;border:1.5px solid var(--border-strong);border-radius:5px;background:var(--surface-card);display:inline-flex;align-items:center;justify-content:center;transition:background .12s,border-color .12s;margin-top:1px}
.mmv-check__box svg{width:12px;height:12px;color:#fff;opacity:0;transform:scale(.6);transition:opacity .12s,transform .12s}
.mmv-check:hover .mmv-check__box{border-color:var(--accent)}
.mmv-check input:checked + .mmv-check__box{background:var(--accent);border-color:var(--accent)}
.mmv-check input:checked + .mmv-check__box svg{opacity:1;transform:scale(1)}
.mmv-check input:indeterminate + .mmv-check__box{background:var(--accent);border-color:var(--accent)}
.mmv-check input:focus-visible + .mmv-check__box{box-shadow:var(--shadow-focus)}
.mmv-check--disabled .mmv-check__box{background:var(--surface-sunken);border-color:var(--border)}
.mmv-check__ind{width:10px;height:2px;background:#fff;border-radius:1px}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-check-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-check-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
const Tick = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "3.5",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "M20 6 9 17l-5-5"
}));
function Checkbox({
  label,
  checked,
  defaultChecked,
  indeterminate = false,
  disabled = false,
  onChange,
  className = '',
  ...rest
}) {
  const ref = React.useRef(null);
  React.useEffect(() => {
    if (ref.current) ref.current.indeterminate = indeterminate;
  }, [indeterminate]);
  return /*#__PURE__*/React.createElement("label", {
    className: ['mmv-check', disabled && 'mmv-check--disabled', className].filter(Boolean).join(' ')
  }, /*#__PURE__*/React.createElement("input", _extends({
    ref: ref,
    type: "checkbox",
    checked: checked,
    defaultChecked: defaultChecked,
    disabled: disabled,
    onChange: onChange
  }, rest)), /*#__PURE__*/React.createElement("span", {
    className: "mmv-check__box"
  }, indeterminate ? /*#__PURE__*/React.createElement("span", {
    className: "mmv-check__ind"
  }) : /*#__PURE__*/React.createElement(Tick, null)), label && /*#__PURE__*/React.createElement("span", null, label));
}
Object.assign(__ds_scope, { Checkbox });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Checkbox.jsx", error: String((e && e.message) || e) }); }

// components/data/Table.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-tablewrap{background:var(--surface-card);border:1px solid var(--border);border-radius:var(--radius-md);overflow:hidden;font-family:var(--font-body)}
.mmv-table{width:100%;border-collapse:collapse;font-size:var(--text-sm)}
.mmv-table thead th{background:var(--surface-sunken);color:var(--text-secondary);font-weight:var(--weight-semibold);
  font-size:var(--text-xs);text-transform:uppercase;letter-spacing:.03em;text-align:left;padding:11px 16px;white-space:nowrap;border-bottom:1px solid var(--border);position:sticky;top:0}
.mmv-table th.mmv-sortable{cursor:pointer;user-select:none}
.mmv-table th.mmv-sortable:hover{color:var(--text-primary)}
.mmv-table__sort{display:inline-flex;align-items:center;gap:6px}
.mmv-table__sort svg{width:13px;height:13px;opacity:.5}
.mmv-table th[data-sorted="true"]{color:var(--accent)}
.mmv-table th[data-sorted="true"] .mmv-table__sort svg{opacity:1}
.mmv-table tbody td{padding:12px 16px;border-bottom:1px solid var(--border);color:var(--text-primary);vertical-align:middle}
.mmv-table tbody tr:last-child td{border-bottom:0}
.mmv-table tbody tr{transition:background .1s}
.mmv-table tbody tr:hover{background:var(--surface-hover)}
.mmv-table tbody tr[data-selected="true"]{background:var(--surface-selected)}
.mmv-table .mmv-col-check{width:44px;padding-right:0}
.mmv-table .align-right{text-align:right}
.mmv-table .align-center{text-align:center}
.mmv-tablefoot{display:flex;align-items:center;justify-content:space-between;gap:12px;padding:12px 16px;border-top:1px solid var(--border);font-size:var(--text-sm);color:var(--text-secondary);flex-wrap:wrap}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-table-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-table-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
const Arrow = ({
  dir
}) => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2.2",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, dir === 'desc' ? /*#__PURE__*/React.createElement("path", {
  d: "m6 9 6 6 6-6"
}) : /*#__PURE__*/React.createElement("path", {
  d: "m18 15-6-6-6 6"
}));
function Table({
  columns = [],
  rows = [],
  rowKey = 'id',
  selectable = false,
  selectedIds,
  onToggleRow,
  onToggleAll,
  sortKey,
  sortDir = 'asc',
  onSort,
  footer,
  className = '',
  ...rest
}) {
  const sel = selectedIds || [];
  const allSel = selectable && rows.length > 0 && rows.every(r => sel.includes(r[rowKey]));
  const someSel = selectable && sel.length > 0 && !allSel;
  return /*#__PURE__*/React.createElement("div", _extends({
    className: ['mmv-tablewrap', className].filter(Boolean).join(' ')
  }, rest), /*#__PURE__*/React.createElement("table", {
    className: "mmv-table"
  }, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, selectable && /*#__PURE__*/React.createElement("th", {
    className: "mmv-col-check"
  }, /*#__PURE__*/React.createElement(__ds_scope.Checkbox, {
    checked: allSel,
    indeterminate: someSel,
    onChange: () => onToggleAll && onToggleAll(!allSel)
  })), columns.map(c => {
    const sorted = sortKey === c.key;
    return /*#__PURE__*/React.createElement("th", {
      key: c.key,
      className: [c.sortable && 'mmv-sortable', c.align && `align-${c.align}`].filter(Boolean).join(' '),
      "data-sorted": sorted,
      style: c.width ? {
        width: c.width
      } : undefined,
      onClick: () => c.sortable && onSort && onSort(c.key, sorted && sortDir === 'asc' ? 'desc' : 'asc')
    }, /*#__PURE__*/React.createElement("span", {
      className: "mmv-table__sort"
    }, c.header, c.sortable && /*#__PURE__*/React.createElement(Arrow, {
      dir: sorted ? sortDir : 'asc'
    })));
  }))), /*#__PURE__*/React.createElement("tbody", null, rows.map(r => {
    const id = r[rowKey];
    const isSel = sel.includes(id);
    return /*#__PURE__*/React.createElement("tr", {
      key: id,
      "data-selected": isSel
    }, selectable && /*#__PURE__*/React.createElement("td", {
      className: "mmv-col-check"
    }, /*#__PURE__*/React.createElement(__ds_scope.Checkbox, {
      checked: isSel,
      onChange: () => onToggleRow && onToggleRow(id)
    })), columns.map(c => /*#__PURE__*/React.createElement("td", {
      key: c.key,
      className: c.align ? `align-${c.align}` : undefined
    }, c.render ? c.render(r) : r[c.key])));
  }))), footer && /*#__PURE__*/React.createElement("div", {
    className: "mmv-tablefoot"
  }, footer));
}
Object.assign(__ds_scope, { Table });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/data/Table.jsx", error: String((e && e.message) || e) }); }

// components/forms/Radio.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-radio{display:inline-flex;align-items:center;gap:10px;cursor:pointer;font-family:var(--font-body);font-size:var(--text-body);color:var(--text-primary)}
.mmv-radio--disabled{cursor:not-allowed;color:var(--text-disabled)}
.mmv-radio input{position:absolute;opacity:0;width:0;height:0}
.mmv-radio__dot{flex:0 0 auto;width:18px;height:18px;border:1.5px solid var(--border-strong);border-radius:50%;background:var(--surface-card);display:inline-flex;align-items:center;justify-content:center;transition:border-color .12s}
.mmv-radio__dot::after{content:"";width:9px;height:9px;border-radius:50%;background:var(--accent);transform:scale(0);transition:transform .12s}
.mmv-radio:hover .mmv-radio__dot{border-color:var(--accent)}
.mmv-radio input:checked + .mmv-radio__dot{border-color:var(--accent)}
.mmv-radio input:checked + .mmv-radio__dot::after{transform:scale(1)}
.mmv-radio input:focus-visible + .mmv-radio__dot{box-shadow:var(--shadow-focus)}
.mmv-radio--disabled .mmv-radio__dot{background:var(--surface-sunken);border-color:var(--border)}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-radio-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-radio-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
function Radio({
  label,
  disabled = false,
  className = '',
  ...rest
}) {
  return /*#__PURE__*/React.createElement("label", {
    className: ['mmv-radio', disabled && 'mmv-radio--disabled', className].filter(Boolean).join(' ')
  }, /*#__PURE__*/React.createElement("input", _extends({
    type: "radio",
    disabled: disabled
  }, rest)), /*#__PURE__*/React.createElement("span", {
    className: "mmv-radio__dot"
  }), label && /*#__PURE__*/React.createElement("span", null, label));
}
Object.assign(__ds_scope, { Radio });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Radio.jsx", error: String((e && e.message) || e) }); }

// components/forms/Switch.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-switch{display:inline-flex;align-items:center;gap:10px;cursor:pointer;font-family:var(--font-body);font-size:var(--text-body);color:var(--text-primary)}
.mmv-switch--disabled{cursor:not-allowed;color:var(--text-disabled)}
.mmv-switch input{position:absolute;opacity:0;width:0;height:0}
.mmv-switch__track{flex:0 0 auto;width:38px;height:22px;border-radius:var(--radius-full);background:var(--border-strong);position:relative;transition:background .15s}
.mmv-switch__track::after{content:"";position:absolute;top:2px;left:2px;width:18px;height:18px;border-radius:50%;background:#fff;box-shadow:0 1px 2px rgba(11,29,58,.3);transition:transform .15s}
.mmv-switch input:checked + .mmv-switch__track{background:var(--accent)}
.mmv-switch input:checked + .mmv-switch__track::after{transform:translateX(16px)}
.mmv-switch input:focus-visible + .mmv-switch__track{box-shadow:var(--shadow-focus)}
.mmv-switch--disabled .mmv-switch__track{opacity:.5}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-switch-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-switch-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
function Switch({
  label,
  disabled = false,
  className = '',
  ...rest
}) {
  return /*#__PURE__*/React.createElement("label", {
    className: ['mmv-switch', disabled && 'mmv-switch--disabled', className].filter(Boolean).join(' ')
  }, /*#__PURE__*/React.createElement("input", _extends({
    type: "checkbox",
    role: "switch",
    disabled: disabled
  }, rest)), /*#__PURE__*/React.createElement("span", {
    className: "mmv-switch__track"
  }), label && /*#__PURE__*/React.createElement("span", null, label));
}
Object.assign(__ds_scope, { Switch });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Switch.jsx", error: String((e && e.message) || e) }); }

// components/forms/fieldParts.jsx
try { (() => {
/* Shared form-field chrome: label, hint, validation message, and the base
   control styling used by Input, Textarea, Select, DateField. Injected once. */
const CSS = `
.mmv-field{display:flex;flex-direction:column;gap:6px}
.mmv-field__label{font-family:var(--font-body);font-size:var(--text-sm);font-weight:var(--weight-medium);color:var(--text-primary)}
.mmv-field__req{color:var(--error);margin-left:3px}
.mmv-field__hint{font-size:var(--text-xs);color:var(--text-tertiary)}
.mmv-field__msg{font-size:var(--text-xs);display:inline-flex;gap:5px;align-items:center;line-height:1.3}
.mmv-field__msg svg{width:13px;height:13px;flex:0 0 auto}
.mmv-field__msg--error{color:var(--error-text)}
.mmv-field__msg--success{color:var(--success-text)}

.mmv-control{
  font-family:var(--font-body);font-size:var(--text-body);color:var(--text-primary);
  background:var(--surface-card);border:1px solid var(--border);border-radius:var(--radius-sm);
  padding:10px 12px;width:100%;transition:border-color .13s,box-shadow .13s,background .13s;
}
.mmv-control::placeholder{color:var(--text-tertiary)}
.mmv-control:hover:not(:disabled){border-color:var(--border-strong)}
.mmv-control:focus{outline:none;border-color:var(--border-focus);box-shadow:var(--shadow-focus)}
.mmv-control:disabled{background:var(--surface-sunken);color:var(--text-disabled);cursor:not-allowed}
.mmv-control--error{border-color:var(--error)}
.mmv-control--error:focus{box-shadow:0 0 0 3px var(--error-soft)}
.mmv-control--success{border-color:var(--success)}
.mmv-control--success:focus{box-shadow:0 0 0 3px var(--success-soft)}
`;
function injectFieldCSS() {
  if (typeof document === 'undefined' || document.getElementById('mmv-field-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-field-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
injectFieldCSS();
const AlertTri = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"
}), /*#__PURE__*/React.createElement("path", {
  d: "M12 9v4"
}), /*#__PURE__*/React.createElement("path", {
  d: "M12 17h.01"
}));
const Check = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2.4",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "M20 6 9 17l-5-5"
}));

/** Wraps a control with its label + validation/hint text. */
function FieldShell({
  label,
  htmlFor,
  required,
  hint,
  error,
  success,
  children,
  className = ''
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: ['mmv-field', className].filter(Boolean).join(' ')
  }, label && /*#__PURE__*/React.createElement("label", {
    className: "mmv-field__label",
    htmlFor: htmlFor
  }, label, required && /*#__PURE__*/React.createElement("span", {
    className: "mmv-field__req"
  }, "*")), children, error ? /*#__PURE__*/React.createElement("span", {
    className: "mmv-field__msg mmv-field__msg--error"
  }, /*#__PURE__*/React.createElement(AlertTri, null), error) : success ? /*#__PURE__*/React.createElement("span", {
    className: "mmv-field__msg mmv-field__msg--success"
  }, /*#__PURE__*/React.createElement(Check, null), success) : hint ? /*#__PURE__*/React.createElement("span", {
    className: "mmv-field__hint"
  }, hint) : null);
}
Object.assign(__ds_scope, { injectFieldCSS, FieldShell });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/fieldParts.jsx", error: String((e && e.message) || e) }); }

// components/forms/DateField.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-datewrap{position:relative;display:flex;align-items:center}
.mmv-datewrap .mmv-control{padding-right:38px}
.mmv-datewrap__ic{position:absolute;right:12px;pointer-events:none;color:var(--text-tertiary);display:inline-flex}
.mmv-datewrap__ic svg{width:16px;height:16px}
.mmv-datewrap input::-webkit-calendar-picker-indicator{opacity:0;cursor:pointer}
.mmv-datewrap input::-webkit-datetime-edit{color:var(--text-primary)}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-date-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-date-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
__ds_scope.injectFieldCSS();
inject();
const Cal = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "M8 2v4"
}), /*#__PURE__*/React.createElement("path", {
  d: "M16 2v4"
}), /*#__PURE__*/React.createElement("rect", {
  width: "18",
  height: "18",
  x: "3",
  y: "4",
  rx: "2"
}), /*#__PURE__*/React.createElement("path", {
  d: "M3 10h18"
}));
let uid = 0;
function DateField({
  label,
  hint,
  error,
  success,
  required,
  id,
  className = '',
  disabled = false,
  ...rest
}) {
  const fid = id || `mmv-date-${++uid}`;
  const state = error ? 'mmv-control--error' : success ? 'mmv-control--success' : '';
  return /*#__PURE__*/React.createElement(__ds_scope.FieldShell, {
    label: label,
    htmlFor: fid,
    required: required,
    hint: hint,
    error: error,
    success: success
  }, /*#__PURE__*/React.createElement("span", {
    className: "mmv-datewrap"
  }, /*#__PURE__*/React.createElement("input", _extends({
    id: fid,
    type: "date",
    className: ['mmv-control', state, className].filter(Boolean).join(' '),
    disabled: disabled
  }, rest)), /*#__PURE__*/React.createElement("span", {
    className: "mmv-datewrap__ic"
  }, /*#__PURE__*/React.createElement(Cal, null))));
}
Object.assign(__ds_scope, { DateField });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/DateField.jsx", error: String((e && e.message) || e) }); }

// components/forms/Input.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-inputwrap{position:relative;display:flex;align-items:center}
.mmv-inputwrap .mmv-control{width:100%}
.mmv-inputwrap--lead .mmv-control{padding-left:38px}
.mmv-inputwrap--trail .mmv-control{padding-right:38px}
.mmv-inputwrap__ic{position:absolute;display:inline-flex;color:var(--text-tertiary);pointer-events:none}
.mmv-inputwrap__ic svg,.mmv-inputwrap__ic i{width:16px;height:16px}
.mmv-inputwrap__ic--lead{left:12px}
.mmv-inputwrap__ic--trail{right:12px}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-input-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-input-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
__ds_scope.injectFieldCSS();
inject();
let uid = 0;
function Input({
  label,
  hint,
  error,
  success,
  required,
  leftIcon,
  rightIcon,
  id,
  className = '',
  disabled = false,
  ...rest
}) {
  const fid = id || `mmv-input-${++uid}`;
  const state = error ? 'mmv-control--error' : success ? 'mmv-control--success' : '';
  const wrapMod = [leftIcon && 'mmv-inputwrap--lead', rightIcon && 'mmv-inputwrap--trail'].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement(__ds_scope.FieldShell, {
    label: label,
    htmlFor: fid,
    required: required,
    hint: hint,
    error: error,
    success: success
  }, /*#__PURE__*/React.createElement("span", {
    className: ['mmv-inputwrap', wrapMod].filter(Boolean).join(' ')
  }, leftIcon && /*#__PURE__*/React.createElement("span", {
    className: "mmv-inputwrap__ic mmv-inputwrap__ic--lead"
  }, leftIcon), /*#__PURE__*/React.createElement("input", _extends({
    id: fid,
    className: ['mmv-control', state, className].filter(Boolean).join(' '),
    disabled: disabled
  }, rest)), rightIcon && /*#__PURE__*/React.createElement("span", {
    className: "mmv-inputwrap__ic mmv-inputwrap__ic--trail"
  }, rightIcon)));
}
Object.assign(__ds_scope, { Input });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Input.jsx", error: String((e && e.message) || e) }); }

// components/forms/SearchBox.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-search{position:relative;display:flex;align-items:center;width:100%}
.mmv-search__ic{position:absolute;left:12px;display:inline-flex;color:var(--text-tertiary);pointer-events:none}
.mmv-search__ic svg{width:16px;height:16px}
.mmv-search input{
  font-family:var(--font-body);font-size:var(--text-body);color:var(--text-primary);
  background:var(--surface-sunken);border:1px solid transparent;border-radius:var(--radius-sm);
  padding:9px 12px 9px 36px;width:100%;transition:border-color .13s,box-shadow .13s,background .13s;
}
.mmv-search input::placeholder{color:var(--text-tertiary)}
.mmv-search input:hover{background:var(--surface-hover)}
.mmv-search input:focus{outline:none;background:var(--surface-card);border-color:var(--border-focus);box-shadow:var(--shadow-focus)}
.mmv-search__clear{position:absolute;right:8px;border:0;background:transparent;color:var(--text-tertiary);cursor:pointer;display:inline-flex;padding:4px;border-radius:var(--radius-xs)}
.mmv-search__clear:hover{color:var(--text-primary);background:var(--surface-hover)}
.mmv-search__clear svg{width:15px;height:15px}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-search-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-search-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
__ds_scope.injectFieldCSS();
inject();
const Glass = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("circle", {
  cx: "11",
  cy: "11",
  r: "8"
}), /*#__PURE__*/React.createElement("path", {
  d: "m21 21-4.3-4.3"
}));
const X = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "M18 6 6 18"
}), /*#__PURE__*/React.createElement("path", {
  d: "m6 6 12 12"
}));
function SearchBox({
  placeholder = 'Rechercher un patient, un dossier…',
  value,
  onChange,
  onClear,
  className = '',
  ...rest
}) {
  const showClear = value != null && value !== '';
  return /*#__PURE__*/React.createElement("div", {
    className: ['mmv-search', className].filter(Boolean).join(' ')
  }, /*#__PURE__*/React.createElement("span", {
    className: "mmv-search__ic"
  }, /*#__PURE__*/React.createElement(Glass, null)), /*#__PURE__*/React.createElement("input", _extends({
    type: "search",
    placeholder: placeholder,
    value: value,
    onChange: onChange
  }, rest)), showClear && /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: "mmv-search__clear",
    "aria-label": "Effacer",
    onClick: onClear
  }, /*#__PURE__*/React.createElement(X, null)));
}
Object.assign(__ds_scope, { SearchBox });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/SearchBox.jsx", error: String((e && e.message) || e) }); }

// components/forms/Select.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-selectwrap{position:relative;display:flex;align-items:center}
.mmv-selectwrap select.mmv-control{appearance:none;-webkit-appearance:none;padding-right:38px;cursor:pointer}
.mmv-selectwrap__chev{position:absolute;right:12px;pointer-events:none;color:var(--text-tertiary);display:inline-flex}
.mmv-selectwrap__chev svg{width:16px;height:16px}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-select-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-select-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
__ds_scope.injectFieldCSS();
inject();
const Chev = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "m6 9 6 6 6-6"
}));
let uid = 0;
function Select({
  label,
  hint,
  error,
  success,
  required,
  id,
  placeholder,
  options,
  className = '',
  disabled = false,
  children,
  ...rest
}) {
  const fid = id || `mmv-sel-${++uid}`;
  const state = error ? 'mmv-control--error' : success ? 'mmv-control--success' : '';
  return /*#__PURE__*/React.createElement(__ds_scope.FieldShell, {
    label: label,
    htmlFor: fid,
    required: required,
    hint: hint,
    error: error,
    success: success
  }, /*#__PURE__*/React.createElement("span", {
    className: "mmv-selectwrap"
  }, /*#__PURE__*/React.createElement("select", _extends({
    id: fid,
    className: ['mmv-control', state, className].filter(Boolean).join(' '),
    disabled: disabled,
    defaultValue: rest.value === undefined ? '' : undefined
  }, rest), placeholder && /*#__PURE__*/React.createElement("option", {
    value: "",
    disabled: true
  }, placeholder), options ? options.map(o => {
    const val = typeof o === 'string' ? o : o.value;
    const lbl = typeof o === 'string' ? o : o.label;
    return /*#__PURE__*/React.createElement("option", {
      key: val,
      value: val
    }, lbl);
  }) : children), /*#__PURE__*/React.createElement("span", {
    className: "mmv-selectwrap__chev"
  }, /*#__PURE__*/React.createElement(Chev, null))));
}
Object.assign(__ds_scope, { Select });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Select.jsx", error: String((e && e.message) || e) }); }

// components/forms/Textarea.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
__ds_scope.injectFieldCSS();
let uid = 0;
function Textarea({
  label,
  hint,
  error,
  success,
  required,
  id,
  rows = 4,
  className = '',
  disabled = false,
  ...rest
}) {
  const fid = id || `mmv-ta-${++uid}`;
  const state = error ? 'mmv-control--error' : success ? 'mmv-control--success' : '';
  return /*#__PURE__*/React.createElement(__ds_scope.FieldShell, {
    label: label,
    htmlFor: fid,
    required: required,
    hint: hint,
    error: error,
    success: success
  }, /*#__PURE__*/React.createElement("textarea", _extends({
    id: fid,
    rows: rows,
    className: ['mmv-control', state, className].filter(Boolean).join(' '),
    style: {
      resize: 'vertical',
      minHeight: 80,
      lineHeight: 'var(--leading-normal)'
    },
    disabled: disabled
  }, rest)));
}
Object.assign(__ds_scope, { Textarea });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Textarea.jsx", error: String((e && e.message) || e) }); }

// components/navigation/Breadcrumb.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-crumbs{display:flex;align-items:center;flex-wrap:wrap;gap:6px;font-family:var(--font-body);font-size:var(--text-sm)}
.mmv-crumbs__item{color:var(--text-secondary);text-decoration:none;background:none;border:0;padding:0;cursor:pointer;font:inherit;transition:color .12s}
.mmv-crumbs__item:hover{color:var(--text-primary)}
.mmv-crumbs__item[aria-current="page"]{color:var(--accent);font-weight:var(--weight-semibold);cursor:default}
.mmv-crumbs__sep{color:var(--text-tertiary);display:inline-flex}
.mmv-crumbs__sep svg{width:14px;height:14px}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-crumbs-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-crumbs-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
const Sep = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "m9 18 6-6-6-6"
}));
function Breadcrumb({
  items = [],
  className = '',
  ...rest
}) {
  return /*#__PURE__*/React.createElement("nav", _extends({
    className: ['mmv-crumbs', className].filter(Boolean).join(' '),
    "aria-label": "Fil d'Ariane"
  }, rest), items.map((it, i) => {
    const last = i === items.length - 1;
    return /*#__PURE__*/React.createElement(React.Fragment, {
      key: i
    }, /*#__PURE__*/React.createElement("a", {
      className: "mmv-crumbs__item",
      href: it.href || undefined,
      "aria-current": last ? 'page' : undefined,
      onClick: it.onClick
    }, it.label), !last && /*#__PURE__*/React.createElement("span", {
      className: "mmv-crumbs__sep"
    }, /*#__PURE__*/React.createElement(Sep, null)));
  }));
}
Object.assign(__ds_scope, { Breadcrumb });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/Breadcrumb.jsx", error: String((e && e.message) || e) }); }

// components/navigation/Modal.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-modal__overlay{position:fixed;inset:0;background:var(--surface-overlay);display:flex;align-items:center;justify-content:center;padding:24px;z-index:1000;animation:mmv-fade .15s ease}
.mmv-modal{background:var(--surface-raised);border:1px solid var(--border);border-radius:var(--radius-md);box-shadow:var(--shadow-3);
  width:100%;max-width:440px;font-family:var(--font-body);animation:mmv-pop .16s ease;max-height:90vh;display:flex;flex-direction:column}
.mmv-modal--lg{max-width:640px}
.mmv-modal__head{display:flex;align-items:flex-start;gap:12px;padding:20px 20px 0}
.mmv-modal__icon{flex:0 0 auto;width:40px;height:40px;border-radius:var(--radius-sm);display:inline-flex;align-items:center;justify-content:center}
.mmv-modal__icon svg{width:20px;height:20px}
.mmv-modal__icon--warning{background:var(--warning-soft);color:var(--warning)}
.mmv-modal__icon--error{background:var(--error-soft);color:var(--error)}
.mmv-modal__icon--accent{background:var(--accent-soft);color:var(--accent)}
.mmv-modal__titles{flex:1 1 auto;min-width:0}
.mmv-modal__title{font-family:var(--font-heading);font-size:var(--text-h3);font-weight:var(--weight-semibold);margin:0;color:var(--text-primary)}
.mmv-modal__x{flex:0 0 auto;border:0;background:transparent;cursor:pointer;color:var(--text-tertiary);padding:4px;border-radius:var(--radius-xs);display:inline-flex}
.mmv-modal__x:hover{color:var(--text-primary);background:var(--surface-hover)}
.mmv-modal__x svg{width:18px;height:18px}
.mmv-modal__body{padding:12px 20px;overflow:auto;color:var(--text-secondary);font-size:var(--text-body);line-height:var(--leading-normal)}
.mmv-modal__foot{display:flex;align-items:center;justify-content:flex-end;gap:10px;padding:16px 20px 20px}
@keyframes mmv-fade{from{opacity:0}to{opacity:1}}
@keyframes mmv-pop{from{opacity:0;transform:translateY(8px) scale(.98)}to{opacity:1;transform:none}}
@media (prefers-reduced-motion:reduce){.mmv-modal,.mmv-modal__overlay{animation:none}}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-modal-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-modal-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
const X = () => /*#__PURE__*/React.createElement("svg", {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "2",
  strokeLinecap: "round",
  strokeLinejoin: "round"
}, /*#__PURE__*/React.createElement("path", {
  d: "M18 6 6 18"
}), /*#__PURE__*/React.createElement("path", {
  d: "m6 6 12 12"
}));
function Modal({
  open = true,
  title,
  icon,
  tone = 'accent',
  size = 'md',
  onClose,
  footer,
  className = '',
  children,
  ...rest
}) {
  if (!open) return null;
  return /*#__PURE__*/React.createElement("div", {
    className: "mmv-modal__overlay",
    onClick: onClose
  }, /*#__PURE__*/React.createElement("div", _extends({
    className: ['mmv-modal', size === 'lg' && 'mmv-modal--lg', className].filter(Boolean).join(' '),
    role: "dialog",
    "aria-modal": "true",
    onClick: e => e.stopPropagation()
  }, rest), /*#__PURE__*/React.createElement("div", {
    className: "mmv-modal__head"
  }, icon && /*#__PURE__*/React.createElement("span", {
    className: `mmv-modal__icon mmv-modal__icon--${tone}`
  }, icon), /*#__PURE__*/React.createElement("div", {
    className: "mmv-modal__titles"
  }, title && /*#__PURE__*/React.createElement("h2", {
    className: "mmv-modal__title"
  }, title)), onClose && /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: "mmv-modal__x",
    "aria-label": "Fermer",
    onClick: onClose
  }, /*#__PURE__*/React.createElement(X, null))), /*#__PURE__*/React.createElement("div", {
    className: "mmv-modal__body"
  }, children), footer && /*#__PURE__*/React.createElement("div", {
    className: "mmv-modal__foot"
  }, footer)));
}
Object.assign(__ds_scope, { Modal });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/Modal.jsx", error: String((e && e.message) || e) }); }

// components/navigation/NavItem.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-nav{display:flex;align-items:center;gap:12px;width:100%;padding:10px 12px;border:0;border-radius:var(--radius-sm);
  background:transparent;color:var(--text-secondary);font-family:var(--font-body);font-size:var(--text-body);font-weight:var(--weight-medium);
  cursor:pointer;text-align:left;transition:background .12s,color .12s;position:relative}
.mmv-nav__ic{flex:0 0 auto;display:inline-flex;width:20px;height:20px;align-items:center;justify-content:center}
.mmv-nav__ic svg,.mmv-nav__ic i{width:20px;height:20px}
.mmv-nav__label{flex:1 1 auto;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.mmv-nav:hover{background:var(--surface-hover);color:var(--text-primary)}
.mmv-nav[data-active="true"]{background:var(--surface-selected);color:var(--accent);font-weight:var(--weight-semibold)}
.mmv-nav[data-active="true"]::before{content:"";position:absolute;left:0;top:8px;bottom:8px;width:3px;border-radius:0 3px 3px 0;background:var(--accent)}
.mmv-nav__badge{flex:0 0 auto;min-width:18px;height:18px;padding:0 5px;border-radius:var(--radius-full);background:var(--error);color:#fff;
  font-size:var(--text-2xs);font-weight:var(--weight-bold);display:inline-flex;align-items:center;justify-content:center}
.mmv-nav--collapsed{justify-content:center;padding:10px}
.mmv-nav--collapsed .mmv-nav__label{display:none}
.mmv-nav--collapsed .mmv-nav__badge{position:absolute;top:5px;right:6px}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-nav-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-nav-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
function NavItem({
  icon,
  label,
  active = false,
  badge,
  collapsed = false,
  onClick,
  className = '',
  ...rest
}) {
  return /*#__PURE__*/React.createElement("button", _extends({
    type: "button",
    className: ['mmv-nav', collapsed && 'mmv-nav--collapsed', className].filter(Boolean).join(' '),
    "data-active": active,
    onClick: onClick,
    title: collapsed ? label : undefined
  }, rest), icon && /*#__PURE__*/React.createElement("span", {
    className: "mmv-nav__ic"
  }, icon), /*#__PURE__*/React.createElement("span", {
    className: "mmv-nav__label"
  }, label), badge != null && badge !== 0 && /*#__PURE__*/React.createElement("span", {
    className: "mmv-nav__badge"
  }, badge));
}
Object.assign(__ds_scope, { NavItem });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/NavItem.jsx", error: String((e && e.message) || e) }); }

// components/navigation/Tabs.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CSS = `
.mmv-tabs{display:flex;align-items:center;gap:4px;border-bottom:1px solid var(--border);font-family:var(--font-body)}
.mmv-tab{position:relative;border:0;background:none;cursor:pointer;font:inherit;font-size:var(--text-body);font-weight:var(--weight-medium);
  color:var(--text-secondary);padding:10px 14px;transition:color .12s;white-space:nowrap;display:inline-flex;align-items:center;gap:8px}
.mmv-tab:hover{color:var(--text-primary)}
.mmv-tab[aria-selected="true"]{color:var(--accent);font-weight:var(--weight-semibold)}
.mmv-tab[aria-selected="true"]::after{content:"";position:absolute;left:8px;right:8px;bottom:-1px;height:2px;border-radius:2px 2px 0 0;background:var(--accent)}
.mmv-tab__count{font-size:var(--text-xs);background:var(--surface-sunken);color:var(--text-secondary);padding:1px 7px;border-radius:var(--radius-full);font-weight:var(--weight-semibold)}
.mmv-tab[aria-selected="true"] .mmv-tab__count{background:var(--accent-soft);color:var(--accent)}
`;
function inject() {
  if (typeof document === 'undefined' || document.getElementById('mmv-tabs-css')) return;
  const s = document.createElement('style');
  s.id = 'mmv-tabs-css';
  s.textContent = CSS;
  document.head.appendChild(s);
}
inject();
function Tabs({
  tabs = [],
  value,
  onChange,
  className = '',
  ...rest
}) {
  return /*#__PURE__*/React.createElement("div", _extends({
    className: ['mmv-tabs', className].filter(Boolean).join(' '),
    role: "tablist"
  }, rest), tabs.map(t => {
    const id = typeof t === 'string' ? t : t.id;
    const label = typeof t === 'string' ? t : t.label;
    const count = typeof t === 'object' ? t.count : undefined;
    return /*#__PURE__*/React.createElement("button", {
      key: id,
      type: "button",
      role: "tab",
      className: "mmv-tab",
      "aria-selected": value === id,
      onClick: () => onChange && onChange(id)
    }, label, count != null && /*#__PURE__*/React.createElement("span", {
      className: "mmv-tab__count"
    }, count));
  }));
}
Object.assign(__ds_scope, { Tabs });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/Tabs.jsx", error: String((e && e.message) || e) }); }

// ui_kits/mmv-desktop/app.jsx
try { (() => {
/* Root app: routing, theme, login gate, notifications. */
const AppNS = window.MMVManageMyVisionDesignSystem_90800d;
function Placeholder({
  route,
  onNav
}) {
  const {
    Button,
    Alert
  } = AppNS;
  const Ic = window.KitIcon;
  const meta = {
    prescriptions: {
      t: 'Ordonnances',
      target: true,
      d: "Le parcours ordonnance existe, mais uniquement depuis la fiche client. Un point d'entrée autonome est une cible V1."
    },
    sales: {
      t: 'Ventes (Point de vente)',
      target: true,
      d: "La vente se lance depuis la fiche client. Un module « Point de vente » autonome est une cible V1."
    },
    reports: {
      t: 'Rapports',
      target: true,
      d: "Module de reporting — à concevoir. Aucune agrégation réelle dans le dépôt actuel."
    },
    products: {
      t: 'Produits',
      target: false,
      d: "CRUD produits (Monture / Verre / Lentille / Accessoire) — non inclus dans cet aperçu de kit."
    },
    inventory: {
      t: 'Inventaire',
      target: false,
      d: "Mouvements de stock et inventaire — non inclus dans cet aperçu de kit."
    }
  }[route] || {
    t: route,
    d: ''
  };
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    className: "page-head"
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("h1", {
    className: "page-title"
  }, meta.t)), meta.target && /*#__PURE__*/React.createElement("span", {
    className: "target-note"
  }, /*#__PURE__*/React.createElement(Ic, {
    n: "wand-2",
    c: "ic16"
  }), " Fonction V1 \xE0 d\xE9velopper")), /*#__PURE__*/React.createElement(Alert, {
    tone: meta.target ? 'warning' : 'info',
    title: meta.target ? 'Cible produit — non implémentée' : 'Écran hors périmètre de cet aperçu'
  }, meta.d), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 20
    }
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "secondary",
    leftIcon: /*#__PURE__*/React.createElement(Ic, {
      n: "arrow-left",
      c: "ic16"
    }),
    onClick: () => onNav('dashboard')
  }, "Retour au tableau de bord")));
}
function NotifModal({
  open,
  onClose
}) {
  const {
    Modal,
    Badge,
    Button
  } = AppNS;
  const Ic = window.KitIcon;
  const D = window.MMV_DATA;
  if (!open) return null;
  return /*#__PURE__*/React.createElement(Modal, {
    open: true,
    tone: "accent",
    icon: /*#__PURE__*/React.createElement(Ic, {
      n: "bell",
      c: "ic20"
    }),
    title: "Notifications",
    onClose: onClose,
    footer: /*#__PURE__*/React.createElement(Button, {
      variant: "tertiary",
      onClick: onClose
    }, "Tout marquer comme lu")
  }, /*#__PURE__*/React.createElement("div", {
    className: "stack",
    style: {
      gap: 2,
      margin: '-4px -4px 0'
    }
  }, D.notifications.map((n, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    className: "acti",
    style: {
      padding: '12px 4px'
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: 'acti__ic ' + n.tone
  }, /*#__PURE__*/React.createElement(Ic, {
    n: n.icon,
    c: "ic18"
  })), /*#__PURE__*/React.createElement("div", {
    className: "acti__b"
  }, /*#__PURE__*/React.createElement("div", {
    className: "acti__t",
    style: {
      fontWeight: n.unread ? 700 : 500
    }
  }, n.text), /*#__PURE__*/React.createElement("div", {
    className: "acti__m"
  }, n.time)), n.unread && /*#__PURE__*/React.createElement("span", {
    style: {
      width: 8,
      height: 8,
      borderRadius: '50%',
      background: 'var(--accent)',
      flex: '0 0 auto'
    }
  })))));
}
function App() {
  const [logged, setLogged] = React.useState(false);
  const [route, setRoute] = React.useState('dashboard');
  const [collapsed, setCollapsed] = React.useState(false);
  const [theme, setTheme] = React.useState(() => localStorage.getItem('mmv-kit-theme') || 'light');
  const [customer, setCustomer] = React.useState(null);
  const [notif, setNotif] = React.useState(false);
  React.useEffect(() => {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem('mmv-kit-theme', theme);
  }, [theme]);
  React.useEffect(() => {
    if (window.lucide) window.lucide.createIcons();
  });
  if (!logged) return /*#__PURE__*/React.createElement(window.KitLogin, {
    onLogin: () => setLogged(true)
  });
  const nav = r => {
    setRoute(r);
  };
  const openCustomer = c => {
    setCustomer(c);
    setRoute('customerDetail');
  };
  let screen;
  if (route === 'dashboard') screen = /*#__PURE__*/React.createElement(window.KitDashboard, {
    onNav: nav
  });else if (route === 'customers') screen = /*#__PURE__*/React.createElement(window.KitCustomers, {
    onOpenCustomer: openCustomer
  });else if (route === 'customerDetail') screen = /*#__PURE__*/React.createElement(window.KitCustomerDetail, {
    customer: customer,
    onBack: () => setRoute('customers'),
    onNav: nav
  });else if (route === 'kanban') screen = /*#__PURE__*/React.createElement(window.KitKanban, null);else screen = /*#__PURE__*/React.createElement(Placeholder, {
    route: route,
    onNav: nav
  });
  return /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement(window.KitShell, {
    route: route,
    onNav: nav,
    collapsed: collapsed,
    onToggleCollapse: () => setCollapsed(v => !v),
    theme: theme,
    onToggleTheme: () => setTheme(t => t === 'dark' ? 'light' : 'dark'),
    onLogout: () => {
      setLogged(false);
      setRoute('dashboard');
    },
    notifCount: 2,
    onOpenNotif: () => setNotif(true)
  }, screen), /*#__PURE__*/React.createElement(NotifModal, {
    open: notif,
    onClose: () => setNotif(false)
  }));
}
ReactDOM.createRoot(document.getElementById('root')).render(/*#__PURE__*/React.createElement(App, null));
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/mmv-desktop/app.jsx", error: String((e && e.message) || e) }); }

// ui_kits/mmv-desktop/customerDetail.jsx
try { (() => {
/* Customer detail. Exposes window.KitCustomerDetail. */
function KitCustomerDetail({
  customer,
  onBack,
  onNav
}) {
  const NS = window.MMVManageMyVisionDesignSystem_90800d;
  const {
    Breadcrumb,
    Tabs,
    Card,
    Button,
    Badge,
    Tag,
    ListItem
  } = NS;
  const Ic = window.KitIcon;
  const D = window.MMV_DATA;
  const c = customer || D.customers[0];
  const [tab, setTab] = React.useState('resume');
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement(Breadcrumb, {
    items: [{
      label: 'Accueil',
      onClick: () => onNav('dashboard')
    }, {
      label: 'Clients',
      onClick: onBack
    }, {
      label: c.name
    }]
  }), /*#__PURE__*/React.createElement("div", {
    className: "detail-head",
    style: {
      marginTop: 18
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "detail-avatar"
  }, c.initials), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("h1", {
    className: "detail-name"
  }, c.name), /*#__PURE__*/React.createElement("div", {
    className: "detail-meta"
  }, /*#__PURE__*/React.createElement("span", null, /*#__PURE__*/React.createElement(Ic, {
    n: "hash",
    c: "ic16"
  }), c.id), /*#__PURE__*/React.createElement("span", null, /*#__PURE__*/React.createElement(Ic, {
    n: "cake",
    c: "ic16"
  }), c.birth), /*#__PURE__*/React.createElement("span", null, /*#__PURE__*/React.createElement(Ic, {
    n: "map-pin",
    c: "ic16"
  }), c.city), c.archived ? /*#__PURE__*/React.createElement(Badge, {
    tone: "neutral",
    dot: true
  }, "Archiv\xE9") : /*#__PURE__*/React.createElement(Badge, {
    tone: "success",
    dot: true
  }, "Client actif"))), /*#__PURE__*/React.createElement("div", {
    className: "head-actions"
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "secondary",
    leftIcon: /*#__PURE__*/React.createElement(Ic, {
      n: "pencil",
      c: "ic16"
    })
  }, "Modifier"), /*#__PURE__*/React.createElement(Button, {
    variant: "primary",
    leftIcon: /*#__PURE__*/React.createElement(Ic, {
      n: "clipboard-plus",
      c: "ic16"
    })
  }, "Nouvelle ordonnance"))), /*#__PURE__*/React.createElement(Tabs, {
    value: tab,
    onChange: setTab,
    tabs: [{
      id: 'resume',
      label: 'Informations'
    }, {
      id: 'rx',
      label: 'Ordonnances',
      count: D.prescriptions.length
    }, {
      id: 'hist',
      label: "Historique d'achats",
      count: D.purchases.length
    }]
  }), /*#__PURE__*/React.createElement("div", {
    className: "tab-body"
  }, tab === 'resume' && /*#__PURE__*/React.createElement("div", {
    className: "two-col"
  }, /*#__PURE__*/React.createElement(Card, {
    title: "Coordonn\xE9es"
  }, /*#__PURE__*/React.createElement("div", {
    className: "info-grid"
  }, /*#__PURE__*/React.createElement("div", {
    className: "info-item"
  }, /*#__PURE__*/React.createElement("div", {
    className: "k"
  }, "Nom complet"), /*#__PURE__*/React.createElement("div", {
    className: "v"
  }, c.name)), /*#__PURE__*/React.createElement("div", {
    className: "info-item"
  }, /*#__PURE__*/React.createElement("div", {
    className: "k"
  }, "Date de naissance"), /*#__PURE__*/React.createElement("div", {
    className: "v"
  }, c.birth)), /*#__PURE__*/React.createElement("div", {
    className: "info-item"
  }, /*#__PURE__*/React.createElement("div", {
    className: "k"
  }, "T\xE9l\xE9phone"), /*#__PURE__*/React.createElement("div", {
    className: "v",
    style: {
      fontVariantNumeric: 'tabular-nums'
    }
  }, c.phone)), /*#__PURE__*/React.createElement("div", {
    className: "info-item"
  }, /*#__PURE__*/React.createElement("div", {
    className: "k"
  }, "Ville"), /*#__PURE__*/React.createElement("div", {
    className: "v"
  }, c.city)), /*#__PURE__*/React.createElement("div", {
    className: "info-item"
  }, /*#__PURE__*/React.createElement("div", {
    className: "k"
  }, "Email"), /*#__PURE__*/React.createElement("div", {
    className: "v"
  }, c.name.split(' ')[0].toLowerCase(), "@email.ma")), /*#__PURE__*/React.createElement("div", {
    className: "info-item"
  }, /*#__PURE__*/React.createElement("div", {
    className: "k"
  }, "R\xE9f\xE9rence"), /*#__PURE__*/React.createElement("div", {
    className: "v",
    style: {
      fontFamily: 'var(--font-mono)'
    }
  }, c.id)))), /*#__PURE__*/React.createElement(Card, {
    title: "\xC9tiquettes"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      flexWrap: 'wrap'
    }
  }, /*#__PURE__*/React.createElement(Tag, null, "Patient"), /*#__PURE__*/React.createElement(Tag, {
    variant: "teal"
  }, "Suivi r\xE9gulier"), /*#__PURE__*/React.createElement(Tag, {
    variant: "outline"
  }, "Progressifs")))), tab === 'rx' && /*#__PURE__*/React.createElement("div", {
    className: "stack",
    style: {
      gap: 14
    }
  }, D.prescriptions.map((p, i) => /*#__PURE__*/React.createElement(Card, {
    key: i,
    title: 'Ordonnance du ' + p.date,
    subtitle: p.doctor,
    headerActions: /*#__PURE__*/React.createElement(Button, {
      variant: "tertiary",
      rightIcon: /*#__PURE__*/React.createElement(Ic, {
        n: "arrow-right",
        c: "ic16"
      })
    }, "D\xE9tails")
  }, /*#__PURE__*/React.createElement("div", {
    className: "rx"
  }, /*#__PURE__*/React.createElement("div", {
    className: "rx-eye"
  }, /*#__PURE__*/React.createElement("div", {
    className: "lbl"
  }, "\u0152il droit (OD)"), /*#__PURE__*/React.createElement("div", {
    className: "val"
  }, p.od)), /*#__PURE__*/React.createElement("div", {
    className: "rx-eye"
  }, /*#__PURE__*/React.createElement("div", {
    className: "lbl"
  }, "\u0152il gauche (OG)"), /*#__PURE__*/React.createElement("div", {
    className: "val"
  }, p.og)))))), tab === 'hist' && /*#__PURE__*/React.createElement("div", {
    className: "stack",
    style: {
      gap: 10
    }
  }, D.purchases.map((p, i) => /*#__PURE__*/React.createElement(ListItem, {
    key: i,
    icon: /*#__PURE__*/React.createElement(Ic, {
      n: "receipt",
      c: "ic18"
    }),
    title: p.items,
    description: p.ref + ' · ' + p.date,
    trailing: /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 12
      }
    }, /*#__PURE__*/React.createElement("b", {
      style: {
        fontVariantNumeric: 'tabular-nums'
      }
    }, p.total), /*#__PURE__*/React.createElement(Badge, {
      tone: p.status === 'Livrée' ? 'success' : 'warning',
      dot: true
    }, p.status))
  })))));
}
window.KitCustomerDetail = KitCustomerDetail;
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/mmv-desktop/customerDetail.jsx", error: String((e && e.message) || e) }); }

// ui_kits/mmv-desktop/customers.jsx
try { (() => {
/* Customers list. Exposes window.KitCustomers. */
function KitCustomers({
  onOpenCustomer
}) {
  const NS = window.MMVManageMyVisionDesignSystem_90800d;
  const {
    Table,
    Badge,
    Button,
    SearchBox,
    Checkbox,
    IconButton,
    EmptyState
  } = NS;
  const Ic = window.KitIcon;
  const D = window.MMV_DATA;
  const [q, setQ] = React.useState('');
  const [inclArch, setInclArch] = React.useState(false);
  const [sortKey, setSortKey] = React.useState('name');
  const [sortDir, setSortDir] = React.useState('asc');
  let rows = D.customers.filter(c => inclArch || !c.archived);
  if (q) rows = rows.filter(c => (c.name + c.id + c.city).toLowerCase().includes(q.toLowerCase()));
  rows = [...rows].sort((a, b) => {
    const r = ('' + a[sortKey]).localeCompare('' + b[sortKey]);
    return sortDir === 'asc' ? r : -r;
  });
  const columns = [{
    key: 'name',
    header: 'Client',
    sortable: true,
    render: r => /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 10
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        width: 32,
        height: 32,
        borderRadius: '50%',
        background: 'var(--accent-soft)',
        color: 'var(--accent)',
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontWeight: 700,
        fontSize: 12,
        flex: '0 0 auto'
      }
    }, r.initials), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
      style: {
        fontWeight: 600
      }
    }, r.name), /*#__PURE__*/React.createElement("div", {
      style: {
        fontSize: 'var(--text-xs)',
        color: 'var(--text-tertiary)',
        fontFamily: 'var(--font-mono)'
      }
    }, r.id)))
  }, {
    key: 'city',
    header: 'Ville',
    sortable: true
  }, {
    key: 'phone',
    header: 'Téléphone',
    render: r => /*#__PURE__*/React.createElement("span", {
      style: {
        fontVariantNumeric: 'tabular-nums'
      }
    }, r.phone)
  }, {
    key: 'status',
    header: 'Statut',
    sortable: true,
    render: r => r.archived ? /*#__PURE__*/React.createElement(Badge, {
      tone: "neutral",
      dot: true
    }, "Archiv\xE9") : /*#__PURE__*/React.createElement(Badge, {
      tone: "success",
      dot: true
    }, "Actif")
  }, {
    key: 'act',
    header: '',
    align: 'right',
    render: r => /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        gap: 2,
        justifyContent: 'flex-end'
      }
    }, /*#__PURE__*/React.createElement(IconButton, {
      variant: "ghost",
      size: "sm",
      "aria-label": "Ouvrir la fiche",
      onClick: () => onOpenCustomer(r)
    }, /*#__PURE__*/React.createElement(Ic, {
      n: "arrow-right",
      c: "ic16"
    })), /*#__PURE__*/React.createElement(IconButton, {
      variant: "ghost",
      size: "sm",
      "aria-label": "Options"
    }, /*#__PURE__*/React.createElement(Ic, {
      n: "more-vertical",
      c: "ic16"
    })))
  }];
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    className: "page-head"
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("h1", {
    className: "page-title"
  }, "Clients"), /*#__PURE__*/React.createElement("p", {
    className: "page-sub"
  }, rows.length, " dossier", rows.length > 1 ? 's' : '', " \xB7 protection d'historique \xE0 l'archivage")), /*#__PURE__*/React.createElement("div", {
    className: "head-actions"
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "secondary",
    leftIcon: /*#__PURE__*/React.createElement(Ic, {
      n: "download",
      c: "ic16"
    })
  }, "Exporter"), /*#__PURE__*/React.createElement(Button, {
    variant: "primary",
    leftIcon: /*#__PURE__*/React.createElement(Ic, {
      n: "user-plus",
      c: "ic16"
    })
  }, "Nouveau client"))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 16,
      alignItems: 'center',
      marginBottom: 16,
      flexWrap: 'wrap'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 260,
      maxWidth: 420
    }
  }, /*#__PURE__*/React.createElement(SearchBox, {
    value: q,
    onChange: e => setQ(e.target.value),
    onClear: () => setQ(''),
    placeholder: "Rechercher par nom, ville, r\xE9f\xE9rence\u2026"
  })), /*#__PURE__*/React.createElement(Checkbox, {
    label: "Inclure les archiv\xE9s",
    checked: inclArch,
    onChange: e => setInclArch(e.target.checked)
  })), rows.length === 0 ? /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'var(--surface-card)',
      border: '1px solid var(--border)',
      borderRadius: 'var(--radius-md)'
    }
  }, /*#__PURE__*/React.createElement(EmptyState, {
    icon: /*#__PURE__*/React.createElement(Ic, {
      n: "user-round-search",
      c: "ic22"
    }),
    title: "Aucun client trouv\xE9",
    description: "Aucun dossier ne correspond \xE0 cette recherche.",
    actions: /*#__PURE__*/React.createElement(Button, {
      variant: "secondary",
      onClick: () => setQ('')
    }, "R\xE9initialiser")
  })) : /*#__PURE__*/React.createElement(Table, {
    columns: columns,
    rows: rows,
    sortKey: sortKey,
    sortDir: sortDir,
    onSort: (k, d) => {
      setSortKey(k);
      setSortDir(d);
    },
    footer: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("span", null, "1\u2013", rows.length, " sur ", rows.length), /*#__PURE__*/React.createElement("span", {
      style: {
        display: 'flex',
        gap: 6,
        alignItems: 'center'
      }
    }, /*#__PURE__*/React.createElement(IconButton, {
      variant: "ghost",
      size: "sm",
      "aria-label": "Pr\xE9c\xE9dent"
    }, /*#__PURE__*/React.createElement(Ic, {
      n: "chevron-left",
      c: "ic16"
    })), /*#__PURE__*/React.createElement("b", {
      style: {
        color: 'var(--accent)',
        padding: '0 6px'
      }
    }, "1"), /*#__PURE__*/React.createElement(IconButton, {
      variant: "ghost",
      size: "sm",
      "aria-label": "Suivant"
    }, /*#__PURE__*/React.createElement(Ic, {
      n: "chevron-right",
      c: "ic16"
    }))))
  }));
}
window.KitCustomers = KitCustomers;
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/mmv-desktop/customers.jsx", error: String((e && e.message) || e) }); }

// ui_kits/mmv-desktop/dashboard.jsx
try { (() => {
/* Dashboard screen. Exposes window.KitDashboard. */
function KitDashboard({
  onNav
}) {
  const NS = window.MMVManageMyVisionDesignSystem_90800d;
  const {
    StatCard,
    Card,
    Button,
    Badge
  } = NS;
  const Ic = window.KitIcon;
  const D = window.MMV_DATA;
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    className: "page-head"
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("h1", {
    className: "page-title"
  }, "Tableau de bord"), /*#__PURE__*/React.createElement("p", {
    className: "page-sub"
  }, "Vue d'ensemble de l'activit\xE9 du magasin")), /*#__PURE__*/React.createElement("div", {
    className: "head-actions"
  }, /*#__PURE__*/React.createElement("span", {
    className: "target-note"
  }, /*#__PURE__*/React.createElement(Ic, {
    n: "wand-2",
    c: "ic16"
  }), " Chiffres de d\xE9monstration"), /*#__PURE__*/React.createElement(Button, {
    variant: "primary",
    leftIcon: /*#__PURE__*/React.createElement(Ic, {
      n: "plus",
      c: "ic16"
    }),
    onClick: () => onNav('customers')
  }, "Nouvelle vente"))), /*#__PURE__*/React.createElement("div", {
    className: "stat-grid"
  }, D.stats.map((s, i) => /*#__PURE__*/React.createElement(StatCard, {
    key: s.key,
    variant: i === 0 ? 'accent' : 'default',
    icon: /*#__PURE__*/React.createElement(Ic, {
      n: s.icon
    }),
    value: s.value,
    label: s.label,
    trend: s.trend,
    trendDirection: s.dir
  }))), /*#__PURE__*/React.createElement("div", {
    className: "two-col"
  }, /*#__PURE__*/React.createElement(Card, {
    title: "Activit\xE9s r\xE9centes",
    headerActions: /*#__PURE__*/React.createElement(Button, {
      variant: "tertiary",
      rightIcon: /*#__PURE__*/React.createElement(Ic, {
        n: "arrow-right",
        c: "ic16"
      })
    }, "Voir tout")
  }, /*#__PURE__*/React.createElement("div", {
    className: "stack"
  }, D.activity.map((a, i) => /*#__PURE__*/React.createElement("div", {
    className: "acti",
    key: i
  }, /*#__PURE__*/React.createElement("span", {
    className: 'acti__ic ' + a.tone
  }, /*#__PURE__*/React.createElement(Ic, {
    n: a.icon,
    c: "ic18"
  })), /*#__PURE__*/React.createElement("div", {
    className: "acti__b"
  }, /*#__PURE__*/React.createElement("div", {
    className: "acti__t"
  }, a.title), /*#__PURE__*/React.createElement("div", {
    className: "acti__m"
  }, a.meta)), /*#__PURE__*/React.createElement("span", {
    className: "acti__time"
  }, a.time))))), /*#__PURE__*/React.createElement("div", {
    className: "stack",
    style: {
      gap: 16
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("h3", {
    style: {
      font: '600 var(--text-h3)/1 var(--font-heading)',
      margin: '0 0 12px'
    }
  }, "Actions rapides"), /*#__PURE__*/React.createElement("div", {
    className: "act-row"
  }, /*#__PURE__*/React.createElement("button", {
    className: "act-item",
    onClick: () => onNav('customers')
  }, /*#__PURE__*/React.createElement("span", {
    className: "act-ic"
  }, /*#__PURE__*/React.createElement(Ic, {
    n: "user-plus",
    c: "ic18"
  })), /*#__PURE__*/React.createElement("span", {
    className: "act-label"
  }, "Nouveau client"), /*#__PURE__*/React.createElement(Ic, {
    n: "chevron-right",
    c: "ic16",
    style: {
      color: 'var(--text-tertiary)'
    }
  })), /*#__PURE__*/React.createElement("button", {
    className: "act-item",
    onClick: () => onNav('kanban')
  }, /*#__PURE__*/React.createElement("span", {
    className: "act-ic"
  }, /*#__PURE__*/React.createElement(Ic, {
    n: "wrench",
    c: "ic18"
  })), /*#__PURE__*/React.createElement("span", {
    className: "act-label"
  }, "Suivi atelier"), /*#__PURE__*/React.createElement(Ic, {
    n: "chevron-right",
    c: "ic16",
    style: {
      color: 'var(--text-tertiary)'
    }
  })), /*#__PURE__*/React.createElement("button", {
    className: "act-item",
    onClick: () => onNav('inventory')
  }, /*#__PURE__*/React.createElement("span", {
    className: "act-ic"
  }, /*#__PURE__*/React.createElement(Ic, {
    n: "boxes",
    c: "ic18"
  })), /*#__PURE__*/React.createElement("span", {
    className: "act-label"
  }, "Inventaire rapide"), /*#__PURE__*/React.createElement(Ic, {
    n: "chevron-right",
    c: "ic16",
    style: {
      color: 'var(--text-tertiary)'
    }
  })))), /*#__PURE__*/React.createElement(Card, {
    elevation: 0,
    style: {
      background: 'var(--warning-soft)',
      borderColor: 'transparent'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement(Ic, {
    n: "alert-triangle",
    c: "ic20",
    style: {
      color: 'var(--warning)',
      flex: '0 0 auto'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 'var(--text-sm)'
    }
  }, /*#__PURE__*/React.createElement("b", null, "1 alerte stock bas"), /*#__PURE__*/React.createElement("div", {
    className: "muted",
    style: {
      marginTop: 2
    }
  }, "Monture Zellige Ac\xE9tate \u2014 4 restants (seuil 5).")))))));
}
window.KitDashboard = KitDashboard;
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/mmv-desktop/dashboard.jsx", error: String((e && e.message) || e) }); }

// ui_kits/mmv-desktop/data.js
try { (() => {
/* MMV Desktop — demo data (fictional, from the UI-PREP handoff §9). */
window.MMV_DATA = {
  user: {
    name: 'Dr. Martin',
    role: 'Administrateur',
    initials: 'DM'
  },
  stats: [{
    key: 'patients',
    icon: 'users',
    value: '1 248',
    label: 'Clients actifs',
    trend: '+12%',
    dir: 'up'
  }, {
    key: 'consult',
    icon: 'clipboard-list',
    value: '342',
    label: 'Consultations',
    trend: '+8%',
    dir: 'up'
  }, {
    key: 'orders',
    icon: 'shopping-bag',
    value: '128',
    label: 'Commandes',
    trend: '+6%',
    dir: 'up'
  }, {
    key: 'atelier',
    icon: 'wrench',
    value: '24',
    label: 'En atelier',
    trend: '+4%',
    dir: 'up'
  }],
  activity: [{
    icon: 'shopping-cart',
    tone: 'success',
    title: 'Vente VTE-000119 — Salma Bouhaddou',
    meta: 'Lentilles mensuelles ×6 · 380,00 MAD',
    time: 'Il y a 2 h'
  }, {
    icon: 'user-plus',
    tone: 'accent',
    title: 'Nouveau client — Yasmine El Amrani',
    meta: 'Fiche créée · Casablanca',
    time: 'Il y a 3 h'
  }, {
    icon: 'wrench',
    tone: 'info',
    title: 'CMD-000092 passée en fabrication',
    meta: 'Omar Benali · livraison estimée 19/07',
    time: 'Il y a 5 h'
  }, {
    icon: 'alert-triangle',
    tone: 'warning',
    title: 'Stock bas — Monture Zellige Acétate',
    meta: '4 restants · seuil 5',
    time: 'Hier'
  }],
  customers: [{
    id: 'PT-000118',
    name: 'Yasmine El Amrani',
    birth: '14/03/1988',
    phone: '06 00 00 12 01',
    city: 'Casablanca',
    status: 'Actif',
    archived: false,
    initials: 'YE'
  }, {
    id: 'PT-000119',
    name: 'Omar Benali',
    birth: '02/11/1975',
    phone: '06 00 00 12 02',
    city: 'Rabat',
    status: 'Actif',
    archived: false,
    initials: 'OB'
  }, {
    id: 'PT-000120',
    name: 'Salma Bouhaddou',
    birth: '21/07/1994',
    phone: '06 00 00 12 03',
    city: 'Marrakech',
    status: 'Actif',
    archived: false,
    initials: 'SB'
  }, {
    id: 'PT-000121',
    name: 'Rachid Tazi',
    birth: '09/01/1962',
    phone: '06 00 00 12 04',
    city: 'Fès',
    status: 'Actif',
    archived: false,
    initials: 'RT'
  }, {
    id: 'PT-000122',
    name: 'Nadia Cherkaoui',
    birth: '30/05/2001',
    phone: '06 00 00 12 05',
    city: 'Tanger',
    status: 'Archivé',
    archived: true,
    initials: 'NC'
  }],
  prescriptions: [{
    date: '12/05/2026',
    doctor: 'Dr. Alaoui',
    od: 'Sph −2.25 · Cyl −0.75 · Axe 175',
    og: 'Sph −2.00 · Cyl −0.50 · Axe 10'
  }, {
    date: '03/06/2026',
    doctor: 'Dr. Sekkat',
    od: 'Sph +1.50 · Add 2.00',
    og: 'Sph +1.75 · Add 2.00'
  }],
  purchases: [{
    ref: 'VTE-000118',
    date: '12/05/2026',
    items: 'Monture Atlas + Verres progressifs',
    total: '1 700,00 MAD',
    status: 'En attente de verres'
  }, {
    ref: 'VTE-000119',
    date: '02/06/2026',
    items: 'Lentilles mensuelles ×6',
    total: '380,00 MAD',
    status: 'Livrée'
  }],
  kanban: [{
    key: 'new',
    title: 'Nouvelle',
    tone: 'neutral',
    cards: [{
      id: 'CMD-000095',
      client: 'Yasmine El Amrani',
      desc: 'Monture + Verres unifocaux',
      badge: 'Nouveau',
      tone: 'neutral'
    }]
  }, {
    key: 'tofab',
    title: 'À fabriquer',
    tone: 'info',
    cards: [{
      id: 'CMD-000091',
      client: 'Yasmine El Amrani',
      desc: 'Monture Atlas + Progressifs 1.67',
      badge: 'Priorité',
      tone: 'warning',
      due: '21/07'
    }]
  }, {
    key: 'fab',
    title: 'En fabrication',
    tone: 'accent',
    cards: [{
      id: 'CMD-000092',
      client: 'Omar Benali',
      desc: 'Monture + Progressifs 1.67',
      badge: 'En cours',
      tone: 'info',
      due: '19/07'
    }]
  }, {
    key: 'qc',
    title: 'Contrôle qualité',
    tone: 'warning',
    cards: [{
      id: 'CMD-000093',
      client: 'Rachid Tazi',
      desc: 'Verres unifocaux 1.60 AR',
      badge: 'À vérifier',
      tone: 'warning',
      due: '16/07'
    }]
  }, {
    key: 'ready',
    title: 'Prête',
    tone: 'success',
    cards: [{
      id: 'CMD-000094',
      client: 'Salma Bouhaddou',
      desc: 'Verres unifocaux + monture',
      badge: 'Prête',
      tone: 'success',
      due: '15/07'
    }]
  }, {
    key: 'done',
    title: 'Livrée',
    tone: 'neutral',
    cards: [{
      id: 'CMD-000090',
      client: 'Fatima Idrissi',
      desc: 'Solaire polarisée',
      badge: 'Livrée',
      tone: 'neutral'
    }]
  }],
  notifications: [{
    icon: 'alert-triangle',
    tone: 'warning',
    text: 'Stock bas : Monture Zellige Acétate (4 restants, seuil 5)',
    time: 'Il y a 1 h',
    unread: true
  }, {
    icon: 'check-circle',
    tone: 'success',
    text: 'CMD-000094 prête pour retrait — Salma Bouhaddou',
    time: 'Il y a 3 h',
    unread: true
  }, {
    icon: 'shopping-cart',
    tone: 'info',
    text: 'Vente VTE-000119 enregistrée',
    time: 'Hier',
    unread: false
  }]
};
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/mmv-desktop/data.js", error: String((e && e.message) || e) }); }

// ui_kits/mmv-desktop/kanban.jsx
try { (() => {
/* Atelier kanban. Exposes window.KitKanban. */
function KitKanban() {
  const NS = window.MMVManageMyVisionDesignSystem_90800d;
  const {
    Badge,
    Button,
    StatusDot
  } = NS;
  const Ic = window.KitIcon;
  const D = window.MMV_DATA;
  const dotTone = {
    neutral: 'neutral',
    info: 'info',
    accent: 'info',
    warning: 'warning',
    success: 'success'
  };
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    className: "page-head"
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("h1", {
    className: "page-title"
  }, "Atelier"), /*#__PURE__*/React.createElement("p", {
    className: "page-sub"
  }, "Suivi de fabrication \u2014 6 statuts align\xE9s sur les commandes")), /*#__PURE__*/React.createElement("div", {
    className: "head-actions"
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "secondary",
    leftIcon: /*#__PURE__*/React.createElement(Ic, {
      n: "printer",
      c: "ic16"
    })
  }, "Fiche de fabrication"), /*#__PURE__*/React.createElement(Button, {
    variant: "primary",
    leftIcon: /*#__PURE__*/React.createElement(Ic, {
      n: "plus",
      c: "ic16"
    })
  }, "Nouvelle commande"))), /*#__PURE__*/React.createElement("div", {
    className: "kanban"
  }, D.kanban.map(col => /*#__PURE__*/React.createElement("div", {
    className: "kcol",
    key: col.key
  }, /*#__PURE__*/React.createElement("div", {
    className: "kcol__head"
  }, /*#__PURE__*/React.createElement("span", {
    className: "kcol__title"
  }, /*#__PURE__*/React.createElement(StatusDot, {
    tone: dotTone[col.tone] || 'neutral',
    pulse: col.key === 'fab'
  }), " ", col.title), /*#__PURE__*/React.createElement("span", {
    className: "kcol__count"
  }, col.cards.length)), col.cards.map(card => /*#__PURE__*/React.createElement("div", {
    className: "kcard",
    key: card.id
  }, /*#__PURE__*/React.createElement("div", {
    className: "kcard__id"
  }, card.id), /*#__PURE__*/React.createElement("div", {
    className: "kcard__client"
  }, card.client), /*#__PURE__*/React.createElement("div", {
    className: "kcard__desc"
  }, card.desc), /*#__PURE__*/React.createElement("div", {
    className: "kcard__foot"
  }, /*#__PURE__*/React.createElement(Badge, {
    tone: card.tone,
    dot: card.tone !== 'neutral'
  }, card.badge), card.due && /*#__PURE__*/React.createElement("span", {
    className: "kcard__due"
  }, /*#__PURE__*/React.createElement(Ic, {
    n: "calendar",
    c: "ic16"
  }), card.due)))), col.cards.length === 0 && /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 'var(--text-xs)',
      color: 'var(--text-tertiary)',
      textAlign: 'center',
      padding: '16px 0'
    }
  }, "Aucune commande")))));
}
window.KitKanban = KitKanban;
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/mmv-desktop/kanban.jsx", error: String((e && e.message) || e) }); }

// ui_kits/mmv-desktop/login.jsx
try { (() => {
/* Login gate. Exposes window.KitLogin. */
const LoginNS = window.MMVManageMyVisionDesignSystem_90800d;
function KitLogin({
  onLogin
}) {
  const {
    Input,
    Button
  } = LoginNS;
  const Ic = window.KitIcon;
  const [u, setU] = React.useState('admin');
  const [p, setP] = React.useState('admin');
  const [err, setErr] = React.useState('');
  const [busy, setBusy] = React.useState(false);
  const submit = e => {
    e && e.preventDefault();
    if (!u || !p) {
      setErr('Veuillez saisir vos identifiants.');
      return;
    }
    setBusy(true);
    setErr('');
    setTimeout(() => {
      setBusy(false);
      onLogin();
    }, 500);
  };
  return /*#__PURE__*/React.createElement("div", {
    className: "login"
  }, /*#__PURE__*/React.createElement("div", {
    className: "login__brand"
  }, /*#__PURE__*/React.createElement("img", {
    className: "login__logo",
    src: "../../assets/logo-dark.png",
    alt: "MMV \u2014 ManageMyVision"
  }), /*#__PURE__*/React.createElement("div", {
    className: "login__tag"
  }, "La gestion int\xE9gr\xE9e de votre magasin d'optique."), /*#__PURE__*/React.createElement("div", {
    className: "login__feats"
  }, /*#__PURE__*/React.createElement("div", {
    className: "login__feat"
  }, /*#__PURE__*/React.createElement("span", {
    className: "fic"
  }, /*#__PURE__*/React.createElement(Ic, {
    n: "check",
    c: "ic16"
  })), " Dossiers patients & ordonnances"), /*#__PURE__*/React.createElement("div", {
    className: "login__feat"
  }, /*#__PURE__*/React.createElement("span", {
    className: "fic"
  }, /*#__PURE__*/React.createElement(Ic, {
    n: "check",
    c: "ic16"
  })), " Ventes, commandes & atelier"), /*#__PURE__*/React.createElement("div", {
    className: "login__feat"
  }, /*#__PURE__*/React.createElement("span", {
    className: "fic"
  }, /*#__PURE__*/React.createElement(Ic, {
    n: "check",
    c: "ic16"
  })), " Stock et fournisseurs en temps r\xE9el"))), /*#__PURE__*/React.createElement("div", {
    className: "login__form-wrap"
  }, /*#__PURE__*/React.createElement("form", {
    className: "login__form",
    onSubmit: submit
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("h1", {
    className: "login__welcome"
  }, "Bienvenue,"), /*#__PURE__*/React.createElement("p", {
    className: "page-sub"
  }, "Veuillez entrer vos identifiants pour continuer.")), /*#__PURE__*/React.createElement(Input, {
    label: "Identifiant",
    value: u,
    onChange: e => setU(e.target.value),
    placeholder: "ex : admin",
    leftIcon: /*#__PURE__*/React.createElement(Ic, {
      n: "user",
      c: "ic16"
    })
  }), /*#__PURE__*/React.createElement(Input, {
    label: "Mot de passe",
    type: "password",
    value: p,
    onChange: e => setP(e.target.value),
    placeholder: "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022",
    leftIcon: /*#__PURE__*/React.createElement(Ic, {
      n: "lock",
      c: "ic16"
    }),
    error: err || undefined
  }), /*#__PURE__*/React.createElement(Button, {
    type: "submit",
    variant: "primary",
    fullWidth: true,
    disabled: busy,
    rightIcon: busy ? undefined : /*#__PURE__*/React.createElement(Ic, {
      n: "arrow-right",
      c: "ic16"
    })
  }, busy ? 'Connexion…' : 'Se connecter'), /*#__PURE__*/React.createElement("div", {
    className: "login__demo"
  }, /*#__PURE__*/React.createElement("b", null, "Mode d\xE9monstration"), " \u2014 Identifiant : ", /*#__PURE__*/React.createElement("code", null, "admin"), " \xB7 Mot de passe : ", /*#__PURE__*/React.createElement("code", null, "admin")))));
}
window.KitLogin = KitLogin;
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/mmv-desktop/login.jsx", error: String((e && e.message) || e) }); }

// ui_kits/mmv-desktop/shell.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/* Shell: sidebar + top bar + status bar. Exposes window.KitShell, window.KitIcon. */
const {
  NavItem
} = window.MMVManageMyVisionDesignSystem_90800d;
const KitIcon = ({
  n,
  c = 'ic20',
  ...p
}) => /*#__PURE__*/React.createElement("i", _extends({
  "data-lucide": n,
  className: c
}, p));
window.KitIcon = KitIcon;
const NAV = [{
  section: 'Principal'
}, {
  key: 'dashboard',
  icon: 'layout-dashboard',
  label: 'Tableau de bord'
}, {
  key: 'customers',
  icon: 'users',
  label: 'Clients'
}, {
  key: 'prescriptions',
  icon: 'clipboard-list',
  label: 'Ordonnances',
  target: true
}, {
  key: 'sales',
  icon: 'shopping-cart',
  label: 'Ventes',
  target: true
}, {
  section: 'Gestion'
}, {
  key: 'products',
  icon: 'box',
  label: 'Produits'
}, {
  key: 'inventory',
  icon: 'boxes',
  label: 'Inventaire'
}, {
  key: 'kanban',
  icon: 'wrench',
  label: 'Atelier'
}, {
  key: 'reports',
  icon: 'bar-chart-3',
  label: 'Rapports',
  target: true
}];
function KitShell({
  route,
  onNav,
  collapsed,
  onToggleCollapse,
  theme,
  onToggleTheme,
  onLogout,
  notifCount,
  onOpenNotif,
  children
}) {
  const active = route === 'customerDetail' ? 'customers' : route;
  return /*#__PURE__*/React.createElement("div", {
    className: 'kit' + (collapsed ? ' collapsed' : '')
  }, /*#__PURE__*/React.createElement("aside", {
    className: 'sb' + (collapsed ? ' collapsed' : '')
  }, /*#__PURE__*/React.createElement("div", {
    className: "sb__brand"
  }, /*#__PURE__*/React.createElement("img", {
    className: "sb__logo",
    src: theme === 'dark' ? '../../assets/logo-dark.png' : '../../assets/logo-horizontal.png',
    alt: "MMV ManageMyVision"
  }), /*#__PURE__*/React.createElement("span", {
    className: "sb__mark"
  }, "M"), /*#__PURE__*/React.createElement("button", {
    className: "sb__collapse",
    onClick: onToggleCollapse,
    "aria-label": "R\xE9duire le menu"
  }, /*#__PURE__*/React.createElement(KitIcon, {
    n: "panel-left-close",
    c: "ic18"
  }))), /*#__PURE__*/React.createElement("nav", {
    className: "sb__nav"
  }, NAV.map((it, i) => it.section ? /*#__PURE__*/React.createElement("div", {
    key: i,
    className: "sb__section"
  }, it.section) : /*#__PURE__*/React.createElement(NavItem, {
    key: it.key,
    collapsed: collapsed,
    icon: /*#__PURE__*/React.createElement(KitIcon, {
      n: it.icon
    }),
    label: /*#__PURE__*/React.createElement("span", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 8
      }
    }, it.label, it.target && !collapsed && /*#__PURE__*/React.createElement(KitIcon, {
      n: "wand-2",
      c: "ic16",
      style: {
        color: 'var(--warning)'
      }
    })),
    active: active === it.key,
    onClick: () => onNav(it.key)
  }))), /*#__PURE__*/React.createElement("div", {
    className: "sb__foot"
  }, /*#__PURE__*/React.createElement("div", {
    className: "sb__user"
  }, /*#__PURE__*/React.createElement("span", {
    className: "sb__avatar"
  }, window.MMV_DATA.user.initials), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "sb__uname"
  }, window.MMV_DATA.user.name), /*#__PURE__*/React.createElement("div", {
    className: "sb__urole"
  }, window.MMV_DATA.user.role)), /*#__PURE__*/React.createElement("button", {
    className: "sb__collapse sb__logoutbtn",
    onClick: onLogout,
    "aria-label": "D\xE9connexion"
  }, /*#__PURE__*/React.createElement(KitIcon, {
    n: "log-out",
    c: "ic18"
  }))))), /*#__PURE__*/React.createElement("div", {
    className: "main"
  }, /*#__PURE__*/React.createElement("header", {
    className: "top"
  }, /*#__PURE__*/React.createElement("div", {
    className: "top__search"
  }, /*#__PURE__*/React.createElement(window.MMVManageMyVisionDesignSystem_90800d.SearchBox, {
    placeholder: "Rechercher un patient, une commande\u2026"
  })), /*#__PURE__*/React.createElement("div", {
    className: "top__spacer"
  }), /*#__PURE__*/React.createElement("div", {
    className: "top__actions"
  }, /*#__PURE__*/React.createElement("button", {
    className: "top__iconbtn",
    onClick: onToggleTheme,
    "aria-label": "Basculer le th\xE8me"
  }, /*#__PURE__*/React.createElement(KitIcon, {
    n: theme === 'dark' ? 'sun' : 'moon'
  })), /*#__PURE__*/React.createElement("button", {
    className: "top__iconbtn",
    onClick: onOpenNotif,
    "aria-label": "Notifications"
  }, /*#__PURE__*/React.createElement(KitIcon, {
    n: "bell"
  }), notifCount > 0 && /*#__PURE__*/React.createElement("span", {
    className: "top__dot"
  })), /*#__PURE__*/React.createElement("div", {
    className: "top__user"
  }, /*#__PURE__*/React.createElement("span", {
    className: "top__ua"
  }, window.MMV_DATA.user.initials), /*#__PURE__*/React.createElement(KitIcon, {
    n: "chevron-down",
    c: "ic16",
    style: {
      color: 'var(--text-tertiary)'
    }
  })))), /*#__PURE__*/React.createElement("main", {
    className: "content"
  }, children), /*#__PURE__*/React.createElement("footer", {
    className: "status"
  }, /*#__PURE__*/React.createElement("span", {
    className: "status__item"
  }, /*#__PURE__*/React.createElement(KitIcon, {
    n: "circle",
    c: "ic16",
    style: {
      color: 'var(--success)'
    }
  }), " Connect\xE9 \xB7 Poste 1"), /*#__PURE__*/React.createElement("span", {
    className: "status__item"
  }, /*#__PURE__*/React.createElement(KitIcon, {
    n: "database",
    c: "ic16"
  }), " Base locale \xB7 \xE0 jour"), /*#__PURE__*/React.createElement("span", {
    className: "status__spacer"
  }), /*#__PURE__*/React.createElement("span", {
    className: "status__item"
  }, "MMV \u2014 ManageMyVision \xB7 v1.0"))));
}
window.KitShell = KitShell;
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/mmv-desktop/shell.jsx", error: String((e && e.message) || e) }); }

__ds_ns.Button = __ds_scope.Button;

__ds_ns.IconButton = __ds_scope.IconButton;

__ds_ns.Card = __ds_scope.Card;

__ds_ns.ListItem = __ds_scope.ListItem;

__ds_ns.StatCard = __ds_scope.StatCard;

__ds_ns.Table = __ds_scope.Table;

__ds_ns.Alert = __ds_scope.Alert;

__ds_ns.Badge = __ds_scope.Badge;

__ds_ns.EmptyState = __ds_scope.EmptyState;

__ds_ns.Spinner = __ds_scope.Spinner;

__ds_ns.StatusDot = __ds_scope.StatusDot;

__ds_ns.Tag = __ds_scope.Tag;

__ds_ns.Toast = __ds_scope.Toast;

__ds_ns.Checkbox = __ds_scope.Checkbox;

__ds_ns.DateField = __ds_scope.DateField;

__ds_ns.Input = __ds_scope.Input;

__ds_ns.Radio = __ds_scope.Radio;

__ds_ns.SearchBox = __ds_scope.SearchBox;

__ds_ns.Select = __ds_scope.Select;

__ds_ns.Switch = __ds_scope.Switch;

__ds_ns.Textarea = __ds_scope.Textarea;

__ds_ns.FieldShell = __ds_scope.FieldShell;

__ds_ns.Breadcrumb = __ds_scope.Breadcrumb;

__ds_ns.Modal = __ds_scope.Modal;

__ds_ns.NavItem = __ds_scope.NavItem;

__ds_ns.Tabs = __ds_scope.Tabs;

})();
