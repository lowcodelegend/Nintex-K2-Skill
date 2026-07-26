(function () {
  'use strict';

  /*! k2style: designer-guard */
  var root = document.documentElement;
  if (!/^\/Runtime\/(?:Runtime\/)?Form\//i.test(location.pathname) ||
      root.classList.contains('designer') ||
      root.getAttribute('data-designer') === 'true') {
    return;
  }
  root.classList.add('k2sp-runtime');

  if (window.__k2spNorthstar) return;
  var config = window.K2SP_NORTHSTAR_CONFIG || {};
  if (config.disabled === true) return;
  window.__k2spNorthstar = {
    version: String(config.version || '1'),
    dirty: false,
    navigationSource: 'fallback',
    stylesReady: false,
    stylesLoaded: false
  };

  var state = window.__k2spNorthstar;
  var CACHE_VERSION_KEY = config.cacheVersionKey || 'northstar:navigation:version';
  var CACHE_PREFIX = config.cachePrefix || 'northstar:navigation:v:';
  var BOOT_TIMEOUT_MS = Number(config.bootTimeoutMilliseconds || 2500);
  var NAV_RECONCILE_TIMEOUT_MS = Number(config.navigationTimeoutMilliseconds || 1800);
  var APPLICATION_STYLE_URL = config.applicationCssUrl ||
    location.origin + '/NorthstarAssets/northstar-homepage.css?v=' + encodeURIComponent(state.version);
  var fallbackNavigation = Array.isArray(config.fallbackNavigation) ? config.fallbackNavigation : [];

  mark('k2sp:boot-start');

  function loadApplicationStyles() {
    var completed = false;
    var link = document.querySelector('link[data-k2sp-application-styles]');
    var appendLink = false;

    function complete(loaded) {
      if (completed) return;
      completed = true;
      state.stylesReady = true;
      state.stylesLoaded = loaded;
      document.documentElement.setAttribute(
        'data-k2sp-application-styles',
        loaded ? 'loaded' : 'fallback'
      );
      mark('k2sp:styles-ready');
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
      link.setAttribute('data-k2sp-application-styles', state.version);
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

  function isNorthstarForm(formName) {
    if (!formName) return false;
    if (Array.isArray(config.formNames) && config.formNames.length) {
      return config.formNames.indexOf(formName) >= 0;
    }
    return !config.formNamePrefix || formName.indexOf(config.formNamePrefix) === 0;
  }

  function pageDefinition(formName) {
    var declared = config.pages && config.pages[formName];
    if (declared) {
      return declared;
    }
    return {
      key: 'command',
      eyebrow: new Intl.DateTimeFormat(undefined, {
        weekday: 'long',
        day: 'numeric',
        month: 'long'
      }).format(new Date()),
      title: config.defaultTitle || 'Good morning',
      subtitle: config.defaultSubtitle || 'Here is what changed, what needs attention, and where work is trending.',
      insightTitle: config.defaultInsightTitle || 'Review the work with the greatest current impact',
      insightBody: config.defaultInsightBody || 'The governed priority queue and operational measures below highlight where attention is needed.',
      insightAction: config.defaultInsightAction || 'Review priority work',
      insightNavigationCode: config.insightNavigationCode || 'CASES'
    };
  }

  function controlText(name) {
    var control = document.querySelector('[name="' + CSS.escape(name) + '"]');
    return control ? String(control.textContent || '').replace(/\s+/g, ' ').trim() : '';
  }

  function journeyControlIndex(control, prefix) {
    var name = control && control.getAttribute('name') || '';
    var match = name.match(new RegExp('^' + prefix + '(\\d+)$', 'i'));
    return match ? Number(match[1]) - 1 : -1;
  }

  function guidedJourneyControls() {
    var progress = Array.prototype.slice.call(
      document.querySelectorAll('[name^="prgJourneyStep"]')
    ).filter(function (control) {
      return journeyControlIndex(control, 'prgJourneyStep') >= 0;
    }).sort(function (left, right) {
      return journeyControlIndex(left, 'prgJourneyStep') -
        journeyControlIndex(right, 'prgJourneyStep');
    });
    if (progress.length < 3) return null;
    var tabBox = document.querySelector('.tab-box.form-tabs');
    var tabs = tabBox && tabBox.querySelector(':scope > ul.tab-box-tabs');
    var anchors = tabs ? Array.prototype.slice.call(tabs.querySelectorAll('a.tab')) : [];
    if (!tabBox || !tabs || anchors.length !== progress.length) return null;
    return {
      tabBox: tabBox,
      tabs: tabs,
      anchors: anchors,
      progress: progress
    };
  }

  function updateJourneySelection(journey) {
    var selectedIndex = journey.anchors.findIndex(function (anchor) {
      return anchor.classList.contains('selected');
    });
    if (selectedIndex < 0) selectedIndex = 0;
    var storedHighest = Number(
      journey.tabBox.getAttribute('data-k2sp-highest-visited') || 0
    );
    journey.highestVisited = Math.max(storedHighest, selectedIndex);
    journey.tabBox.setAttribute(
      'data-k2sp-highest-visited',
      String(journey.highestVisited)
    );

    journey.anchors.forEach(function (anchor, index) {
      var stateName = index < selectedIndex ? 'done' :
        index === selectedIndex ? 'current' : 'upcoming';
      anchor.setAttribute('data-k2sp-step-state', stateName);
      anchor.setAttribute('aria-current', index === selectedIndex ? 'step' : 'false');
      var nativeHidden = anchor.style.display === 'none';
      var locked = index > journey.highestVisited || nativeHidden;
      anchor.setAttribute('data-k2sp-step-locked', locked ? 'true' : 'false');
      anchor.setAttribute('aria-disabled', locked ? 'true' : 'false');
    });

    var guidance = journey.tabBox.querySelector(':scope > .k2sp-journey-guidance');
    var guidanceBody = guidance && guidance.querySelector('.k2sp-journey-guidance-body');
    if (guidanceBody) {
      guidanceBody.textContent =
        controlText('dlbJourneyStepDescription' + (selectedIndex + 1)) ||
        'Complete this screen with the clearest information currently available.';
    }
  }

  function enhanceGuidedJourney(page) {
    var journey = guidedJourneyControls();
    if (!journey) return false;
    if (journey.tabBox.classList.contains('k2sp-guided-journey')) {
      updateJourneySelection(journey);
      return true;
    }

    var wasDeclaredInitiation = page.key === 'initiation';
    page.key = 'initiation';
    document.body.classList.remove('k2sp-page-command', 'k2sp-page-workspace');
    document.body.classList.add('k2sp-page-initiation');
    journey.tabBox.classList.add('k2sp-guided-journey');
    journey.tabs.classList.add('k2sp-journey-stepper');
    journey.tabBox.setAttribute('data-k2sp-highest-visited', '0');

    var title = controlText('lblJourneyTitle1') ||
      String(document.title || '').replace(/^[A-Z0-9]{2,4}\./, '').trim();
    var description = controlText('dlbJourneyDescription1') ||
      controlText('dlbJourneyStepDescription1');
    var intro = document.querySelector('#k2sp-shell .k2sp-page-intro');
    var eyebrow = intro && intro.querySelector('.k2sp-eyebrow');
    var heading = intro && intro.querySelector('h1');
    var subtitle = intro && intro.querySelector('p');
    if (eyebrow) eyebrow.textContent = wasDeclaredInitiation && page.eyebrow ?
      page.eyebrow : 'Guided intake';
    if (heading && title) heading.textContent = title;
    if (subtitle && description) subtitle.textContent = description;

    journey.progress.forEach(function (progress, index) {
      var progressCell = progress.closest('.editor-cell');
      var table = progress.closest('[name^="tblJourneyProgress"]');
      var headingControl = document.querySelector(
        '[name="lblJourneyStepHeading' + (index + 1) + '"]'
      );
      var descriptionControl = document.querySelector(
        '[name="dlbJourneyStepDescription' + (index + 1) + '"]'
      );
      var journeyTitleControl = document.querySelector(
        '[name="lblJourneyTitle' + (index + 1) + '"]'
      );
      var journeyDescriptionControl = document.querySelector(
        '[name="dlbJourneyDescription' + (index + 1) + '"]'
      );
      if (table) table.classList.add('k2sp-journey-screen-copy');
      if (progressCell) progressCell.classList.add('k2sp-native-progress-source');
      if (journeyTitleControl && journeyTitleControl.closest('.editor-cell')) {
        journeyTitleControl.closest('.editor-cell').classList.add('k2sp-journey-shell-copy');
      }
      if (journeyDescriptionControl && journeyDescriptionControl.closest('.editor-cell')) {
        journeyDescriptionControl.closest('.editor-cell').classList.add('k2sp-journey-shell-copy');
      }
      if (headingControl && headingControl.closest('.editor-cell')) {
        headingControl.closest('.editor-cell').classList.add('k2sp-journey-screen-title');
      }
      if (descriptionControl && descriptionControl.closest('.editor-cell')) {
        descriptionControl.closest('.editor-cell').classList.add('k2sp-journey-screen-description');
      }
      var panel = progress.closest('.formpanel');
      var primaryView = null;
      if (panel) {
        var panelViews = Array.prototype.slice.call(panel.querySelectorAll('.view'));
        primaryView = panelViews.find(function (view) {
          return panelTitle(view).toLowerCase() !==
            String(config.navigationViewTitle || 'Application navigation').toLowerCase();
        }) || null;
      }
      if (primaryView) primaryView.classList.add('k2sp-journey-primary-view');
    });

    journey.anchors.forEach(function (anchor, index) {
      var labelText = String(anchor.innerText || anchor.textContent || '')
        .replace(/\s+/g, ' ').trim();
      anchor.setAttribute('data-k2sp-step-number', String(index + 1));
      anchor.setAttribute(
        'data-k2sp-step-summary',
        controlText('dlbJourneyStepDescription' + (index + 1))
      );
      anchor.textContent = '';
      anchor.appendChild(create('span', 'k2sp-step-number', String(index + 1)));
      var stepCopy = create('span', 'k2sp-step-copy');
      stepCopy.appendChild(create('b', 'k2sp-step-label', labelText));
      stepCopy.appendChild(create(
        'small',
        'k2sp-step-summary',
        controlText('dlbJourneyStepDescription' + (index + 1))
      ));
      anchor.appendChild(stepCopy);
      anchor.addEventListener('click', function (event) {
        if (anchor.getAttribute('data-k2sp-step-locked') !== 'true') return;
        event.preventDefault();
        event.stopImmediatePropagation();
      }, true);
    });

    var guidance = create('aside', 'k2sp-journey-guidance');
    guidance.setAttribute('aria-label', 'Screen guidance');
    guidance.appendChild(create('h2', '', 'Why we ask this'));
    guidance.appendChild(create('p', 'k2sp-journey-guidance-body', ''));
    var guidanceList = create('ul', '');
    [
      'Use clear, observable facts.',
      'Add reference identifiers where known.',
      'Save the draft before leaving the journey.'
    ].forEach(function (item) {
      guidanceList.appendChild(create('li', '', item));
    });
    guidance.appendChild(guidanceList);
    journey.tabBox.appendChild(guidance);

    var observer = new MutationObserver(function () {
      updateJourneySelection(journey);
    });
    journey.anchors.forEach(function (anchor) {
      observer.observe(anchor, { attributes: true, attributeFilter: ['class', 'style'] });
    });
    updateJourneySelection(journey);
    mark('k2sp:guided-journey-ready');
    return true;
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

  function navigationItem(items, code) {
    var expected = String(code || '').toUpperCase();
    for (var index = 0; index < items.length; index += 1) {
      if (String(items[index].NavigationCode || '').toUpperCase() === expected) return items[index];
    }
    return null;
  }

  function configureShellActions(items) {
    var newCase = navigationItem(items, config.newCaseNavigationCode || 'NEW_CASE');
    var insight = navigationItem(items, config.insightNavigationCode || 'CASES');
    var createLink = document.querySelector('#k2sp-shell [data-k2sp-shell-action="new-case"]');
    var insightLink = document.querySelector('#k2sp-shell [data-k2sp-shell-action="insight"]');

    [[createLink, newCase], [insightLink, insight]].forEach(function (pair) {
      var link = pair[0];
      var item = pair[1];
      if (!link) return;
      if (!item) {
        link.removeAttribute('href');
        link.setAttribute('aria-disabled', 'true');
        return;
      }
      link.href = runtimeUrl(item.TargetFormName);
      link.setAttribute('data-k2sp-route', item.TargetFormName);
      link.setAttribute('data-k2sp-code', item.NavigationCode);
      link.removeAttribute('aria-disabled');
    });
  }

  function buildShell(formName, page, items) {
    var shell = create('div', 'k2sp-shell');
    shell.id = 'k2sp-shell';
    shell.setAttribute('data-k2sp-form', formName);

    var sidebar = create('aside', 'k2sp-sidebar');
    sidebar.setAttribute('aria-label', 'Application navigation');
    sidebar.setAttribute('data-k2sp-shell-region', 'navigation');

    var brand = create('div', 'k2sp-brand');
    var brandMark = create('span', 'k2sp-brand-mark', config.brandMark || 'N');
    var brandCopy = create('span', 'k2sp-brand-copy');
    var brandName = create('b', '', config.brandLabel || 'Northstar');
    var brandSub = create('small', '', config.brandSubtitle || 'Quality operations');
    brandCopy.appendChild(brandName);
    brandCopy.appendChild(brandSub);
    brand.appendChild(brandMark);
    brand.appendChild(brandCopy);

    var nav = create('nav', 'k2sp-nav');
    nav.setAttribute('aria-label', 'Primary');
    var user = create('div', 'k2sp-user');
    var userAvatar = create('span', 'k2sp-user-avatar', config.userInitials || 'AM');
    var userCopy = create('span', 'k2sp-user-copy');
    userCopy.appendChild(create('b', '', config.userName || 'Alex Morgan'));
    userCopy.appendChild(create('small', '', config.userRole || 'Quality manager'));
    user.appendChild(userAvatar);
    user.appendChild(userCopy);
    user.appendChild(create('span', 'k2sp-user-more', '•••'));
    sidebar.appendChild(brand);
    sidebar.appendChild(nav);
    sidebar.appendChild(user);

    var topbar = create('header', 'k2sp-topbar');
    topbar.setAttribute('data-k2sp-shell-region', 'topbar');
    var paletteHost = create('div', 'k2sp-command-palette-host');
    paletteHost.setAttribute('data-k2sp-shell-region', 'command-palette');
    var fallbackSearch = create('div', 'k2sp-search k2sp-search-fallback');
    fallbackSearch.setAttribute('aria-label', 'Search is available from the command centre');
    fallbackSearch.appendChild(create('span', '', '⌕'));
    fallbackSearch.appendChild(create('span', '', 'Search or jump to…'));
    fallbackSearch.appendChild(create('kbd', '', 'Ctrl K'));
    paletteHost.appendChild(fallbackSearch);
    var actions = create('div', 'k2sp-top-actions');
    var notifications = create('button', 'k2sp-icon-button k2sp-notifications', '◇');
    notifications.type = 'button';
    notifications.setAttribute('aria-label', 'Notifications');
    notifications.setAttribute('aria-disabled', 'true');
    var createCase = create('a', 'k2sp-button k2sp-button-primary');
    createCase.appendChild(create('span', 'k2sp-button-label', '＋ New case'));
    createCase.setAttribute('data-k2sp-shell-action', 'new-case');
    actions.appendChild(notifications);
    actions.appendChild(createCase);
    topbar.appendChild(paletteHost);
    topbar.appendChild(actions);

    var intro = create('section', 'k2sp-page-intro');
    intro.setAttribute('aria-labelledby', 'k2sp-page-title');
    var introCopy = create('div', 'k2sp-page-intro-copy');
    var eyebrow = create('div', 'k2sp-eyebrow', page.eyebrow);
    var h1 = create('h1', '', page.title);
    h1.id = 'k2sp-page-title';
    var subtitle = create('p', '', page.subtitle);
    introCopy.appendChild(eyebrow);
    introCopy.appendChild(h1);
    introCopy.appendChild(subtitle);
    intro.appendChild(introCopy);
    if (page.key === 'command') {
      var dashboardActions = create('div', 'k2sp-dashboard-actions');
      var period = create('div', 'k2sp-segmented');
      period.setAttribute('role', 'group');
      period.setAttribute('aria-label', 'Dashboard period');
      [
        { label: '7d', active: false },
        { label: '30d', active: true },
        { label: '90d', active: false }
      ].forEach(function (definition) {
        var periodButton = create('button', definition.active ? 'active' : '', definition.label);
        periodButton.type = 'button';
        periodButton.setAttribute('aria-pressed', definition.active ? 'true' : 'false');
        periodButton.addEventListener('click', function () {
          Array.prototype.forEach.call(period.querySelectorAll('button'), function (button) {
            var selected = button === periodButton;
            button.classList.toggle('active', selected);
            button.setAttribute('aria-pressed', selected ? 'true' : 'false');
          });
          showToast('Dashboard period set to ' + definition.label + '.');
        });
        period.appendChild(periodButton);
      });
      var exportButton = create('button', 'k2sp-button k2sp-export-brief', 'Export brief');
      exportButton.type = 'button';
      exportButton.addEventListener('click', function () {
        window.print();
      });
      dashboardActions.appendChild(period);
      dashboardActions.appendChild(exportButton);
      intro.appendChild(dashboardActions);
    }

    var insight = create('section', 'k2sp-insight');
    var insightIcon = create('div', 'k2sp-insight-icon', '✦');
    insightIcon.setAttribute('aria-hidden', 'true');
    var insightCopy = create('div', 'k2sp-insight-copy');
    insightCopy.appendChild(create('strong', '', page.insightTitle));
    insightCopy.appendChild(create('p', '', page.insightBody));
    var insightButton = create('a', 'k2sp-button k2sp-button-quiet');
    insightButton.appendChild(create('span', 'k2sp-button-label', page.insightAction + ' →'));
    insightButton.setAttribute('data-k2sp-shell-action', 'insight');
    insight.appendChild(insightIcon);
    insight.appendChild(insightCopy);
    insight.appendChild(insightButton);

    var transition = create('div', 'k2sp-transition');
    transition.id = 'k2sp-transition';
    transition.setAttribute('aria-live', 'polite');
    transition.setAttribute('aria-hidden', 'true');
    var transitionMark = create('span', 'k2sp-transition-mark', config.brandMark || 'N');
    var transitionText = create('span', 'k2sp-transition-text', 'Opening workspace…');
    transition.appendChild(transitionMark);
    transition.appendChild(transitionText);

    shell.appendChild(sidebar);
    shell.appendChild(topbar);
    shell.appendChild(intro);
    shell.appendChild(insight);
    shell.appendChild(transition);

    document.body.insertBefore(shell, document.body.firstChild);
    renderNavigation(items, formName);
    configureShellActions(items);
    mark('k2sp:shell-ready');
  }

  function renderNavigation(items, formName) {
    var nav = document.querySelector('#k2sp-shell .k2sp-nav');
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
    var hasExactActiveRoute = ordered.some(function (item) {
      return item.TargetFormName === formName;
    });
    ordered.forEach(function (item, index) {
      if (String(item.NavigationCode || '').toUpperCase() ===
          String(config.newCaseNavigationCode || 'NEW_CASE').toUpperCase()) {
        return;
      }
      var nextSection = item.SectionLabel || '';
      if (nextSection && nextSection !== section) {
        nav.appendChild(create('div', 'k2sp-nav-label', nextSection));
        section = nextSection;
      }

      var link = create('a', 'k2sp-nav-item');
      link.href = runtimeUrl(item.TargetFormName);
      link.setAttribute('data-k2sp-route', item.TargetFormName);
      link.setAttribute('data-k2sp-code', item.NavigationCode || '');
      link.setAttribute('aria-label', item.Label || item.TargetFormName);
      var active = item.TargetFormName === formName ||
        (!hasExactActiveRoute && index === 0 && document.body.classList.contains('k2sp-page-command'));
      if (active) {
        link.classList.add('active');
        link.setAttribute('aria-current', 'page');
      }

      var icon = create('span', 'k2sp-nav-icon', iconText(item.IconToken));
      icon.setAttribute('aria-hidden', 'true');
      var label = create('span', 'k2sp-nav-text', item.Label || item.TargetFormName);
      link.appendChild(icon);
      link.appendChild(label);
      var count = config.navigationCounts && config.navigationCounts[item.NavigationCode];
      if (count !== undefined && count !== null && count !== '') {
        link.appendChild(create('span', 'k2sp-nav-count', String(count)));
      }
      nav.appendChild(link);
    });
    configureShellActions(ordered);
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
      if (!code || !target || (config.formNamePrefix && target.indexOf(config.formNamePrefix) !== 0)) continue;
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
    row.classList.add('k2sp-native-navigation-source');
    row.setAttribute('aria-hidden', 'true');
  }

  function suppressNavigationSource() {
    var expectedTitle = String(config.navigationViewTitle || 'Application navigation').toLowerCase();
    var directTitles = document.querySelectorAll('[data-sf-title]');
    for (var i = 0; i < directTitles.length; i += 1) {
      var value = (
        directTitles[i].getAttribute('data-sf-title') ||
        directTitles[i].textContent ||
        ''
      ).replace(/\s+/g, ' ').trim().toLowerCase();
      if (value !== expectedTitle) continue;
      hideNavigationSource(directTitles[i].closest('.view'));
      return directTitles[i].closest('.view');
    }

    var view = findViewByTitle(config.navigationViewTitle || 'Application navigation');
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
      mark('k2sp:navigation-reconciled');
    }

    function finish(items, view) {
      if (reconciled || !items.length) return false;
      reconciled = true;
      state.navigationSource = 'smartobject';
      document.body.setAttribute('data-k2sp-navigation-source', state.navigationSource);
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
      document.body.setAttribute('data-k2sp-navigation-source', state.navigationSource);
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
    form.classList.add('k2sp-application-content');
    suppressConfiguredFrameworkViews();

    if (enhanceGuidedJourney(page)) {
      form.classList.add('k2sp-initiation-workspace');
      alignNarrowNativeForm(form);
      return;
    }

    alignNarrowNativeForm(form);

    if (page.key === 'command') {
      if (config.enableDashboardComposition === false) return;
      form.classList.add('k2sp-dashboard-grid');
      [
        [config.kpiViewTitle || 'Operational position', 'k2sp-kpis'],
        [config.trendViewTitle || 'Case intake trend', 'k2sp-trend'],
        [config.attentionViewTitle || 'Urgent work', 'k2sp-attention'],
        [config.stagesViewTitle || 'Open cases by stage', 'k2sp-stages'],
        [config.supplierSignalViewTitle || 'Supplier signal', 'k2sp-supplier-signal'],
        [config.trendDataViewTitle || 'Case intake trend data', 'k2sp-data-alternative'],
        [config.attentionDataViewTitle || 'Urgent work data', 'k2sp-data-alternative'],
        [config.stagesDataViewTitle || 'Open cases by stage data', 'k2sp-data-alternative'],
        [config.supplierSignalDataViewTitle || 'Supplier signal data', 'k2sp-data-alternative']
      ].forEach(function (definition) {
        var view = findViewByTitle(definition[0]);
        var row = view && (view.closest('.row') || view);
        if (row) row.classList.add(definition[1]);
      });
      moveCommandPalette();
      transformKpis();
    } else {
      form.classList.add('k2sp-workspace');
      var priority = findViewByTitle('Priority work');
      var priorityRow = priority && (priority.closest('.row') || priority);
      if (priorityRow) priorityRow.classList.add('k2sp-priority-work');
    }
  }

  function suppressConfiguredFrameworkViews() {
    var names = Array.isArray(config.suppressedFrameworkViews)
      ? config.suppressedFrameworkViews
      : [];
    names.forEach(function (name) {
      var source = document.querySelector('[name="' + CSS.escape(name) + '"]');
      var view = source && source.closest('.view');
      var row = view && (view.closest('.row') || view);
      if (row) {
        row.classList.add('k2sp-suppressed-framework-view');
        row.setAttribute('aria-hidden', 'true');
      }
    });
    var panelNames = Array.isArray(config.suppressedFrameworkPanelNames)
      ? config.suppressedFrameworkPanelNames
      : [];
    panelNames.forEach(function (name) {
      var panel = document.querySelector('.panel[name="' + CSS.escape(name) + '"]');
      var row = panel && (panel.closest('.row') || panel);
      if (row) {
        row.classList.add('k2sp-suppressed-framework-view');
        row.setAttribute('aria-hidden', 'true');
      }
    });
  }

  function alignNarrowNativeForm(form) {
    if (!form) return;
    if (window.innerWidth > 800) {
      document.body.style.setProperty('--k2sp-narrow-form-shift', '0px');
      return;
    }
    var boundary = document.body.classList.contains('k2sp-page-initiation')
      ? document.querySelector('#k2sp-shell .k2sp-page-intro')
      : document.querySelector('#k2sp-shell .k2sp-insight');
    if (!boundary) return;
    var minimumTop = boundary.getBoundingClientRect().bottom + 16;
    var runtime = document.querySelector('.runtime-form') || form;
    var formTop = runtime.getBoundingClientRect().top;
    var adjusted = Math.max(0, Math.ceil(minimumTop - formTop));
    document.body.style.setProperty('--k2sp-narrow-form-shift', adjusted + 'px');
  }

  function moveCommandPalette() {
    var host = document.querySelector('#k2sp-shell .k2sp-command-palette-host');
    if (!host) return false;
    var existing = document.querySelector('.k2sp-command-palette-row');
    if (existing) return true;
    var view = findViewByTitle(config.commandPaletteViewTitle || 'Command palette');
    var row = view && (view.closest('.row') || view);
    if (!row) return false;
    row.classList.add('k2sp-command-palette-row');
    host.classList.add('k2sp-command-palette-native');
    return true;
  }

  function layoutKpiCells() {
    var keys = ['OpenCaseCount', 'SLAAtRiskCount', 'OverdueActionCount', 'FirstPassYieldPercent'];
    keys.forEach(function (key, index) {
      var label = document.querySelector('[name="lbl' + key + '"]');
      var value = document.querySelector('[name="dlb' + key + '"]');
      var labelCell = label && label.closest('.editor-cell');
      var valueCell = value && value.closest('.editor-cell');
      if (!labelCell || !valueCell) return;
      var column;
      var labelRow;
      var valueRow;
      if (window.innerWidth <= 480) {
        column = 1;
        labelRow = index * 2 + 1;
        valueRow = labelRow + 1;
      } else if (window.innerWidth <= 1100) {
        column = index % 2 + 1;
        labelRow = Math.floor(index / 2) * 2 + 1;
        valueRow = labelRow + 1;
      } else {
        column = index + 1;
        labelRow = 1;
        valueRow = 2;
      }
      labelCell.style.setProperty('grid-column', String(column), 'important');
      valueCell.style.setProperty('grid-column', String(column), 'important');
      labelCell.style.setProperty('grid-row', String(labelRow), 'important');
      valueCell.style.setProperty('grid-row', String(valueRow), 'important');
    });
  }

  function transformKpis() {
    var keys = ['OpenCaseCount', 'SLAAtRiskCount', 'OverdueActionCount', 'FirstPassYieldPercent'];
    var first = document.querySelector('[name="lblOpenCaseCount"]');
    if (!first) return false;
    var source = first.closest('.root-table');
    if (!source) return false;

    var icons = ['▤', '◷', '↗', '◆'];
    keys.forEach(function (key, index) {
      var label = document.querySelector('[name="lbl' + key + '"]');
      var value = document.querySelector('[name="dlb' + key + '"]');
      if (!label || !value) return;
      var labelCell = label.closest('.editor-cell');
      var valueCell = value.closest('.editor-cell');
      if (!labelCell || !valueCell) return;
      labelCell.classList.add('k2sp-kpi-cell', 'k2sp-kpi-label-cell', 'k2sp-kpi-index-' + (index + 1), 'k2sp-tone-' + index);
      valueCell.classList.add('k2sp-kpi-cell', 'k2sp-kpi-value-cell', 'k2sp-kpi-index-' + (index + 1), 'k2sp-tone-' + index);
      labelCell.setAttribute('data-k2sp-kpi-index', String(index + 1));
      labelCell.setAttribute('data-k2sp-kpi-icon', icons[index]);
      valueCell.setAttribute('data-k2sp-kpi-index', String(index + 1));
      var decoration = config.kpiDecorations && config.kpiDecorations[key];
      if (decoration && decoration.text) {
        valueCell.setAttribute('data-k2sp-kpi-delta', decoration.text);
        valueCell.setAttribute('data-k2sp-kpi-tone', decoration.tone || 'neutral');
      }
    });
    source.classList.add('k2sp-kpi-native-grid');
    layoutKpiCells();
    return true;
  }

  function showTransition(label) {
    var curtain = document.getElementById('k2sp-transition');
    if (!curtain) return;
    var text = curtain.querySelector('.k2sp-transition-text');
    if (text) text.textContent = 'Opening ' + label + '…';
    curtain.setAttribute('aria-hidden', 'false');
    document.body.classList.add('k2sp-leaving');
  }

  function bindRouteNavigation(formName) {
    document.addEventListener('click', function (event) {
      var link = event.target.closest && event.target.closest('[data-k2sp-route]');
      if (!link) return;
      if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      var targetForm = link.getAttribute('data-k2sp-route');
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
      if (target.closest('#k2sp-shell') || target.closest('.k2sp-native-navigation-source')) return;
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

  function revealWhenReady(page) {
    var revealed = false;
    var started = Date.now();

    function reveal() {
      if (revealed) return;
      revealed = true;
      requestAnimationFrame(function () {
        requestAnimationFrame(function () {
          classifyNativeContent(page);
          document.body.classList.add('k2sp-ready');
          mark('k2sp:content-ready');
          [100, 300, 700, 1500].forEach(function (delay) {
            window.setTimeout(function () {
              classifyNativeContent(page);
            }, delay);
          });
        });
      });
    }

    function ready() {
      classifyNativeContent(page);
      if (page.key === 'initiation') {
        return !!document.querySelector('.k2sp-guided-journey') &&
          !!document.querySelector('.k2sp-journey-guidance');
      }
      if (page.key === 'command') {
        if (config.enableDashboardComposition === false) {
          return !!document.querySelector('[name="dlbOpenCaseCount"]');
        }
        return !!document.querySelector('.k2sp-kpi-native-grid') &&
          !!document.querySelector('.k2sp-command-palette-row');
      }
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
    if (!document.body || document.getElementById('k2sp-shell')) return;
    var formName = currentFormName();
    if (!isNorthstarForm(formName)) return;

    var page = pageDefinition(formName);
    var cached = readNavigationCache();
    var initialNavigation = cached || fallbackNavigation;
    state.navigationSource = cached ? 'cache' : 'fallback';

    document.body.classList.add('k2sp-spike', 'k2sp-page-' + page.key);
    document.body.setAttribute('data-k2sp-version', state.version);
    document.body.setAttribute('data-k2sp-navigation-source', state.navigationSource);
    suppressNavigationSource();
    buildShell(formName, page, initialNavigation);
    bindRouteNavigation(formName);
    bindDirtyTracking();
    reconcileNavigation(formName);
    revealWhenReady(page);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', activate);
  } else {
    activate();
  }
})();
