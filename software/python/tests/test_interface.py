from harp.device import hobgoblin, core


def test_device_identity_matches_the_metadata():
    assert hobgoblin.DEVICE_NAME == "Hobgoblin"
    assert hobgoblin.WHO_AM_I == 123


def test_register_map_covers_the_core_and_application_registers():
    # Application registers start at address 32, so the map is only complete if
    # the core registers were merged into it as well.
    assert hobgoblin.REGISTER_MAP
    assert all(address in hobgoblin.REGISTER_MAP for address in core.REGISTER_MAP)
    assert any(address >= 32 for address in hobgoblin.REGISTER_MAP)
