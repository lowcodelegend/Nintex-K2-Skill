(function () {
  'use strict';

  if (!/^\/Runtime\/(?:Runtime\/)?Form\//i.test(location.pathname)) return;
  document.documentElement.classList.add('gux-runtime');

  if (window.__guxNorthstar) return;
  window.__guxNorthstar = {
    version: '2026.07.23.5',
    dirty: false,
    navigationSource: 'fallback',
    stylesReady: false,
    stylesLoaded: false
  };

  var state = window.__guxNorthstar;
  var CACHE_VERSION_KEY = 'gux:navigation:version';
  var CACHE_PREFIX = 'gux:navigation:v:';
  var BOOT_TIMEOUT_MS = 2500;
  var NAV_RECONCILE_TIMEOUT_MS = 1800;
  var APPLICATION_STYLE_URL =
    location.origin + '/GUXAssets/gux-northstar-app.css?v=' + encodeURIComponent(state.version);
  var fallbackNavigation = [
    {
      NavigationCode: 'COMMAND',
      SectionLabel: 'Workspace',
      Label: 'Command centre',
      IconToken: 'home',
      TargetFormName: 'GUX.Gold Command Centre',
      SortOrder: 10,
      IsActive: true,
      ConfigurationVersion: '1'
    },
    {
      NavigationCode: 'MY_WORK',
      SectionLabel: 'Workspace',
      Label: 'My work',
      IconToken: 'work',
      TargetFormName: 'GUX.My Work',
      SortOrder: 20,
      IsActive: true,
      ConfigurationVersion: '1'
    }
  ];

  mark('gux:boot-start');

  function loadApplicationStyles() {
    var completed = false;
    var link = document.querySelector('link[data-gux-application-styles]');
    var appendLink = false;

    function complete(loaded) {
      if (completed) return;
      completed = true;
      state.stylesReady = true;
      state.stylesLoaded = loaded;
      document.documentElement.setAttribute(
        'data-gux-application-styles',
        loaded ? 'loaded' : 'fallback'
      );
      mark('gux:styles-ready');
    }

    if (link) {
      if (link.sheet) {
        complete(true);
        return;
      }
    } else {
      link = document.createElement('link');
      link.rel = 'stylesheet';
      link.href = APPLICATION_STYLE_URL;
      link.setAttribute('data-gux-application-styles', state.version);
      appendLink = true;
    }

    link.addEventListener('load', function () {
      complete(true);
    }, { once: true });
    link.addEventListener('error', function () {
      complete(false);
    }, { once: true });
    if (appendLink) {
      (document.head || document.documentElement).appendChild(link);
    }
    window.setTimeout(function () {
      complete(false);
    }, BOOT_TIMEOUT_MS);
  }

  loadApplicationStyles();

  function mark(name) {
    try {
      if (window.performance && performance.mark) performance.mark(name);
    } catch (_) {}
  }

  function create(tag, className, text) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    if (typeof text === 'string') node.textContent = text;
    return node;
  }

  function currentFormName() {
    var match = location.pathname.match(/\/Form\/([^/?#]+)/i);
    if (!match) return '';
    try {
      return decodeURIComponent(match[1].replace(/\+/g, ' ')).replace(/\/$/, '');
    } catch (_) {
      return match[1].replace(/%20/gi, ' ');
    }
  }

  function runtimeUrl(formName) {
    return location.origin + '/Runtime/Runtime/Form/' + encodeURIComponent(formName) + '/';
  }

  function isGuxForm(formName) {
    return formName === 'GUX.Gold Command Centre' || formName === 'GUX.My Work';
  }

  function pageDefinition(formName) {
    if (formName === 'GUX.My Work') {
      return {
        key: 'my-work',
        eyebrow: 'Focused execution',
        title: 'Work that needs you',
        subtitle: 'Prioritised cases and assigned workflow tasks, together in one place.',
        insightTitle: 'Start with the cases closest to customer or SLA impact',
        insightBody: 'The priority queue is ordered for operational attention. Your K2 tasks remain available on the My Tasks tab.',
        insightAction: 'Review priority work'
      };
    }

    return {
      key: 'command',
      eyebrow: new Intl.DateTimeFormat(undefined, {
        weekday: 'long',
        day: 'numeric',
        month: 'long'
      }).format(new Date()),
      title: 'Good morning, Alex',
      subtitle: 'Here is what changed, what needs attention, and where quality is trending.',
      insightTitle: 'Three related defects may share a machining cause',
      insightBody: 'Cases from Apex Precision Metals reference the same cell, alloy batch, and surface condition. Estimated exposure: 1,840 units.',
      insightAction: 'Review cluster'
    };
  }

  function readNavigationCache() {
    try {
      var version = sessionStorage.getItem(CACHE_VERSION_KEY);
      if (!version) return null;
      var raw = sessionStorage.getItem(CACHE_PREFIX + version);
      if (!raw) return null;
      var parsed = JSON.parse(raw);
      return Array.isArray(parsed) && parsed.length ? parsed : null;
    } catch (_) {
      return null;
    }
  }

  function writeNavigationCache(items) {
    if (!items || !items.length) return;
    var version = String(items[0].ConfigurationVersion || '1');
    try {
      sessionStorage.setItem(CACHE_VERSION_KEY, version);
      sessionStorage.setItem(CACHE_PREFIX + version, JSON.stringify(items));
    } catch (_) {}
  }

  function iconText(token) {
    var icons = {
      home: '⌂',
      work: '✓',
      cases: '▤',
      actions: '↗',
      reports: '◫',
      suppliers: '◇',
      settings: '⚙'
    };
    return icons[String(token || '').toLowerCase()] || '•';
  }

  function buildShell(formName, page, items) {
    var shell = create('div', 'gux-shell');
    shell.id = 'gux-shell';
    shell.setAttribute('data-gux-form', formName);

    var sidebar = create('aside', 'gux-sidebar');
    sidebar.setAttribute('aria-label', 'Application navigation');
    sidebar.setAttribute('data-gux-shell-region', 'navigation');

    var brand = create('div', 'gux-brand');
    var brandMark = create('span', 'gux-brand-mark', 'N');
    var brandCopy = create('span', 'gux-brand-copy');
    var brandName = create('b', '', 'Northstar');
    var brandSub = create('small', '', 'Quality operations');
    brandCopy.appendChild(brandName);
    brandCopy.appendChild(brandSub);
    brand.appendChild(brandMark);
    brand.appendChild(brandCopy);

    var nav = create('nav', 'gux-nav');
    nav.setAttribute('aria-label', 'Primary');
    sidebar.appendChild(brand);
    sidebar.appendChild(nav);

    var topbar = create('header', 'gux-topbar');
    topbar.setAttribute('data-gux-shell-region', 'topbar');
    var search = create('button', 'gux-search');
    search.type = 'button';
    search.setAttribute('aria-label', 'Search cases, suppliers, and lots');
    search.innerHTML = '<span aria-hidden="true">⌕</span><span>Search cases, suppliers, lots…</span><kbd>/</kbd>';
    var actions = create('div', 'gux-top-actions');
    var help = create('button', 'gux-icon-button', '?');
    help.type = 'button';
    help.setAttribute('aria-label', 'Help');
    var createCase = create('button', 'gux-button gux-button-primary', '＋ New case');
    createCase.type = 'button';
    createCase.setAttribute('data-gux-placeholder-action', 'new-case');
    actions.appendChild(help);
    actions.appendChild(createCase);
    topbar.appendChild(search);
    topbar.appendChild(actions);

    var intro = create('section', 'gux-page-intro');
    intro.setAttribute('aria-labelledby', 'gux-page-title');
    var eyebrow = create('div', 'gux-eyebrow', page.eyebrow);
    var h1 = create('h1', '', page.title);
    h1.id = 'gux-page-title';
    var subtitle = create('p', '', page.subtitle);
    intro.appendChild(eyebrow);
    intro.appendChild(h1);
    intro.appendChild(subtitle);

    var insight = create('section', 'gux-insight');
    var insightIcon = create('div', 'gux-insight-icon', '✦');
    insightIcon.setAttribute('aria-hidden', 'true');
    var insightCopy = create('div', 'gux-insight-copy');
    insightCopy.appendChild(create('strong', '', page.insightTitle));
    insightCopy.appendChild(create('p', '', page.insightBody));
    var insightButton = create('button', 'gux-button gux-button-quiet', page.insightAction + ' →');
    insightButton.type = 'button';
    insightButton.setAttribute('data-gux-placeholder-action', 'insight');
    insight.appendChild(insightIcon);
    insight.appendChild(insightCopy);
    insight.appendChild(insightButton);

    var transition = create('div', 'gux-transition');
    transition.id = 'gux-transition';
    transition.setAttribute('aria-live', 'polite');
    transition.setAttribute('aria-hidden', 'true');
    var transitionMark = create('span', 'gux-transition-mark', 'N');
    var transitionText = create('span', 'gux-transition-text', 'Opening workspace…');
    transition.appendChild(transitionMark);
    transition.appendChild(transitionText);

    shell.appendChild(sidebar);
    shell.appendChild(topbar);
    shell.appendChild(intro);
    shell.appendChild(insight);
    shell.appendChild(transition);

    document.body.insertBefore(shell, document.body.firstChild);
    renderNavigation(items, formName);
    mark('gux:shell-ready');
  }

  function renderNavigation(items, formName) {
    var nav = document.querySelector('#gux-shell .gux-nav');
    if (!nav) return;
    nav.textContent = '';

    var ordered = items
      .filter(function (item) {
        return item && item.IsActive !== false && item.TargetFormName;
      })
      .sort(function (a, b) {
        return Number(a.SortOrder || 0) - Number(b.SortOrder || 0);
      });

    var section = null;
    ordered.forEach(function (item) {
      var nextSection = item.SectionLabel || '';
      if (nextSection && nextSection !== section) {
        nav.appendChild(create('div', 'gux-nav-label', nextSection));
        section = nextSection;
      }

      var link = create('a', 'gux-nav-item');
      link.href = runtimeUrl(item.TargetFormName);
      link.setAttribute('data-gux-route', item.TargetFormName);
      link.setAttribute('data-gux-code', item.NavigationCode || '');
      link.setAttribute('aria-label', item.Label || item.TargetFormName);
      var active = item.TargetFormName === formName;
      if (active) {
        link.classList.add('active');
        link.setAttribute('aria-current', 'page');
      }

      var icon = create('span', 'gux-nav-icon', iconText(item.IconToken));
      icon.setAttribute('aria-hidden', 'true');
      var label = create('span', 'gux-nav-text', item.Label || item.TargetFormName);
      link.appendChild(icon);
      link.appendChild(label);
      nav.appendChild(link);
    });
  }

  function panelTitle(view) {
    var titles = view.querySelectorAll(
      '[data-sf-title], .panel-header-text, .panel-header-text span, .header .title'
    );
    for (var i = 0; i < titles.length; i += 1) {
      var value = (
        titles[i].getAttribute('data-sf-title') ||
        titles[i].textContent ||
        ''
      ).replace(/\s+/g, ' ').trim();
      if (value) return value;
    }
    return '';
  }

  function findViewByTitle(title) {
    var views = document.querySelectorAll('.runtime-form .view, .form .view');
    var expected = String(title || '').replace(/\s+/g, ' ').trim().toLowerCase();
    for (var i = 0; i < views.length; i += 1) {
      if (panelTitle(views[i]).toLowerCase() === expected) return views[i];
    }
    return null;
  }

  function cellValue(cell) {
    if (!cell) return '';
    var input = cell.querySelector('input:not([type="checkbox"]), textarea, select');
    if (input && typeof input.value === 'string') return input.value.trim();
    var checked = cell.querySelector('input[type="checkbox"]');
    if (checked) return checked.checked ? 'true' : 'false';
    return (cell.textContent || '').replace(/\s+/g, ' ').trim();
  }

  function booleanValue(value) {
    return !/^(false|0|no|inactive)$/i.test(String(value || '').trim());
  }

  function extractNavigation(view) {
    if (!view) return [];
    var rows = view.querySelectorAll('table.grid-content-table tr, .grid-content-table tr');
    var items = [];

    for (var i = 0; i < rows.length; i += 1) {
      var cells = rows[i].querySelectorAll('td');
      if (cells.length < 6) continue;
      var code = cellValue(cells[0]);
      var target = cellValue(cells[4]);
      if (!code || !target || target.indexOf('GUX.') !== 0) continue;
      items.push({
        NavigationCode: code,
        SectionLabel: cellValue(cells[1]),
        Label: cellValue(cells[2]),
        IconToken: cellValue(cells[3]),
        TargetFormName: target,
        SortOrder: Number(cellValue(cells[5]) || 0),
        IsActive: cells.length > 6 ? booleanValue(cellValue(cells[6])) : true,
        ConfigurationVersion: cells.length > 7 ? cellValue(cells[7]) || '1' : '1'
      });
    }

    return items;
  }

  function hideNavigationSource(view) {
    if (!view) return;
    var row = view.closest('.row') || view;
    row.classList.add('gux-native-navigation-source');
    row.setAttribute('aria-hidden', 'true');
  }

  function suppressNavigationSource() {
    var directTitles = document.querySelectorAll('[data-sf-title]');
    for (var i = 0; i < directTitles.length; i += 1) {
      var value = (
        directTitles[i].getAttribute('data-sf-title') ||
        directTitles[i].textContent ||
        ''
      ).replace(/\s+/g, ' ').trim().toLowerCase();
      if (value !== 'application navigation') continue;
      hideNavigationSource(directTitles[i].closest('.view'));
      return directTitles[i].closest('.view');
    }

    var view = findViewByTitle('Application navigation');
    hideNavigationSource(view);
    return view;
  }

  function reconcileNavigation(formName) {
    var reconciled = false;
    var reconciliationMarked = false;
    var observer;
    var timer;
    var cleanupTimer;

    function markReconciliation() {
      if (reconciliationMarked) return;
      reconciliationMarked = true;
      mark('gux:navigation-reconciled');
    }

    function finish(items, view) {
      if (reconciled || !items.length) return false;
      reconciled = true;
      state.navigationSource = 'smartobject';
      document.body.setAttribute('data-gux-navigation-source', state.navigationSource);
      writeNavigationCache(items);
      renderNavigation(items, formName);
      hideNavigationSource(view);
      if (observer) observer.disconnect();
      if (timer) clearTimeout(timer);
      if (cleanupTimer) clearTimeout(cleanupTimer);
      markReconciliation();
      return true;
    }

    function attempt() {
      var view = suppressNavigationSource();
      return finish(extractNavigation(view), view);
    }

    if (attempt()) return;
    observer = new MutationObserver(function () {
      suppressNavigationSource();
      window.clearTimeout(state.reconcileDebounce);
      state.reconcileDebounce = window.setTimeout(attempt, 40);
    });
    observer.observe(document.documentElement, { childList: true, subtree: true });
    timer = window.setTimeout(function () {
      if (reconciled) return;
      state.navigationSource = readNavigationCache() ? 'cache' : 'fallback';
      document.body.setAttribute('data-gux-navigation-source', state.navigationSource);
      markReconciliation();
    }, NAV_RECONCILE_TIMEOUT_MS);
    cleanupTimer = window.setTimeout(function () {
      suppressNavigationSource();
      if (observer) observer.disconnect();
    }, 15000);
  }

  function classifyNativeContent(page) {
    var form = document.querySelector('.runtime-form .form') || document.querySelector('.form');
    if (!form) return;
    form.classList.add('gux-application-content');

    if (page.key === 'command') {
      form.classList.add('gux-dashboard-grid');
      [
        ['Operational position', 'gux-kpis'],
        ['Nonconformance trend', 'gux-trend'],
        ['Attention now', 'gux-attention'],
        ['Where work is accumulating', 'gux-stages']
      ].forEach(function (definition) {
        var view = findViewByTitle(definition[0]);
        var row = view && (view.closest('.row') || view);
        if (row) row.classList.add(definition[1]);
      });
      transformKpis();
    } else {
      form.classList.add('gux-workspace');
      var priority = findViewByTitle('Priority work');
      var priorityRow = priority && (priority.closest('.row') || priority);
      if (priorityRow) priorityRow.classList.add('gux-priority-work');
    }
  }

  function transformKpis() {
    if (document.querySelector('.gux-kpi-grid')) return;
    var keys = ['OpenCaseCount', 'SLAAtRiskCount', 'OverdueActionCount', 'HighRiskCaseCount'];
    var first = document.querySelector('[name="lblOpenCaseCount"]');
    if (!first) return;
    var source = first.closest('.root-table');
    var host = source && source.parentElement;
    if (!source || !host) return;

    var icons = ['▤', '◷', '↗', '◆'];
    var grid = create('div', 'gux-kpi-grid');
    keys.forEach(function (key, index) {
      var label = document.querySelector('[name="lbl' + key + '"]');
      var value = document.querySelector('[name="dlb' + key + '"]');
      if (!label || !value) return;
      var card = create('div', 'gux-kpi-card gux-tone-' + index);
      card.appendChild(create('span', 'gux-kpi-icon', icons[index]));
      card.appendChild(label);
      card.appendChild(value);
      grid.appendChild(card);
    });
    source.style.display = 'none';
    host.appendChild(grid);
  }

  function showTransition(label) {
    var curtain = document.getElementById('gux-transition');
    if (!curtain) return;
    var text = curtain.querySelector('.gux-transition-text');
    if (text) text.textContent = 'Opening ' + label + '…';
    curtain.setAttribute('aria-hidden', 'false');
    document.body.classList.add('gux-leaving');
  }

  function bindRouteNavigation(formName) {
    document.addEventListener('click', function (event) {
      var link = event.target.closest && event.target.closest('[data-gux-route]');
      if (!link) return;
      if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      var targetForm = link.getAttribute('data-gux-route');
      if (!targetForm || targetForm === formName) {
        event.preventDefault();
        return;
      }

      if (state.dirty && !window.confirm('You have unsaved changes. Leave this page?')) {
        event.preventDefault();
        return;
      }

      event.preventDefault();
      state.dirty = false;
      showTransition((link.textContent || targetForm).replace(/\s+/g, ' ').trim());
      window.setTimeout(function () {
        location.assign(link.href);
      }, 80);
    });
  }

  function bindDirtyTracking() {
    function markDirty(event) {
      if (!event.isTrusted) return;
      var target = event.target;
      if (!target || !target.closest) return;
      if (target.closest('#gux-shell') || target.closest('.gux-native-navigation-source')) return;
      if (target.matches('input, textarea, select, [contenteditable="true"]')) state.dirty = true;
    }
    document.addEventListener('input', markDirty, true);
    document.addEventListener('change', markDirty, true);
    document.addEventListener('keydown', function (event) {
      if (
        event.key === 'Tab' ||
        event.key === 'Shift' ||
        event.key === 'Control' ||
        event.key === 'Alt' ||
        event.key === 'Meta' ||
        event.key.indexOf('Arrow') === 0
      ) {
        return;
      }
      markDirty(event);
    }, true);
    window.addEventListener('beforeunload', function (event) {
      if (!state.dirty) return;
      event.preventDefault();
      event.returnValue = '';
    });
  }

  function bindPlaceholderFeedback() {
    document.addEventListener('click', function (event) {
      var button = event.target.closest && event.target.closest('[data-gux-placeholder-action]');
      if (!button) return;
      button.blur();
      var existing = document.querySelector('.gux-toast');
      if (existing) existing.remove();
      var toast = create(
        'div',
        'gux-toast',
        button.getAttribute('data-gux-placeholder-action') === 'new-case'
          ? 'New case initiation remains owned by the existing SNC application.'
          : 'This insight action is a prototype seam for the case workspace.'
      );
      toast.setAttribute('role', 'status');
      document.body.appendChild(toast);
      window.setTimeout(function () {
        toast.classList.add('visible');
      }, 10);
      window.setTimeout(function () {
        toast.classList.remove('visible');
        window.setTimeout(function () {
          toast.remove();
        }, 220);
      }, 2800);
    });
  }

  function revealWhenReady(page) {
    var revealed = false;
    var started = Date.now();

    function reveal() {
      if (revealed) return;
      revealed = true;
      requestAnimationFrame(function () {
        requestAnimationFrame(function () {
          document.body.classList.add('gux-ready');
          mark('gux:content-ready');
        });
      });
    }

    function ready() {
      classifyNativeContent(page);
      if (page.key === 'command') return !!document.querySelector('[name="dlbOpenCaseCount"]');
      return !!findViewByTitle('Priority work') || !!document.querySelector('.tab-box, .tab-box-body');
    }

    (function poll() {
      var nativeReady = ready();
      if (
        (nativeReady && state.stylesReady) ||
        Date.now() - started >= BOOT_TIMEOUT_MS
      ) {
        reveal();
        return;
      }
      window.setTimeout(poll, 50);
    })();
    window.setTimeout(reveal, BOOT_TIMEOUT_MS);
  }

  function activate() {
    if (!document.body || document.getElementById('gux-shell')) return;
    var formName = currentFormName();
    if (!isGuxForm(formName)) return;

    var page = pageDefinition(formName);
    var cached = readNavigationCache();
    var initialNavigation = cached || fallbackNavigation;
    state.navigationSource = cached ? 'cache' : 'fallback';

    document.body.classList.add('gux-spike', 'gux-page-' + page.key);
    document.body.setAttribute('data-gux-version', state.version);
    document.body.setAttribute('data-gux-navigation-source', state.navigationSource);
    suppressNavigationSource();
    buildShell(formName, page, initialNavigation);
    bindRouteNavigation(formName);
    bindDirtyTracking();
    bindPlaceholderFeedback();
    reconcileNavigation(formName);
    revealWhenReady(page);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', activate);
  } else {
    activate();
  }
})();
