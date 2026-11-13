(() => {
  const menu = document.getElementById('personal-menu');
  const toggle = document.querySelector('[aria-controls="personal-menu"]');
  if (!menu || !toggle) return;
  // Bootstrap xử lý Escape, backdrop và focus; đồng bộ trạng thái cho trình đọc màn hình.
  menu.addEventListener('show.bs.offcanvas', () => toggle.setAttribute('aria-expanded', 'true'));
  menu.addEventListener('hide.bs.offcanvas', () => toggle.setAttribute('aria-expanded', 'false'));
})();
