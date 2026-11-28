-- عميل السوق: عند طاولة الأدوات (داخل مقر العصابة) اضغط H لفتح السوق في لوحة التحكم.
KC.addInteraction(function(ctx)
  if not KC.enabled('market') then return end
  for _, p in ipairs(Config.Places) do
    if p.type == 'gang' then
      local b = KC.pt(p, 'bench')
      if b and ctx.near(b, 2.0) then ctx.line({ key = 'H', text = 'السوق السوداء (أسلحة ومعدات وشحنات)' }, nil, nil, function() KC.open('market') end) end
    end
  end
end)
