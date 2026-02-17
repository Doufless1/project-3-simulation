"""
Unit Tests — Domain entities, value objects, and infrastructure.

Coverage: Tests entity validation, state machines, G-code generation,
          cost calculation, and audit logging.
"""

import unittest
from lab_control.domain.value_objects import (
    Position2D, Speed, FlowRate, GCodeLine, ScanRecipe, EquipmentSpec,
)
from lab_control.domain.entities import (
    XYTable, LaserUnit, GasSystem, SafetySystem,
    TableState, LaserState, GasState, InterlockStatus,
)
from lab_control.domain.exceptions import (
    TableLimitError, TableNotHomedError, LaserPowerError,
    LaserInterlockError, GasFlowError,
)
from lab_control.infrastructure.virtual_table_controller import (
    VirtualTableController,
)
from lab_control.infrastructure.equipment_catalog import (
    EquipmentCostCatalog,
)
from lab_control.infrastructure.audit_logger import InMemoryAuditLogger
from lab_control.composition_root import create_lab_control


# ══════════════════════════════════════════════════════════════════════
#                     VALUE OBJECT TESTS
# ══════════════════════════════════════════════════════════════════════

class TestPosition2D(unittest.TestCase):
    """Test Position2D value object integrity."""

    def test_creation(self):
        pos = Position2D(10.0, 20.0)
        self.assertEqual(pos.x_mm, 10.0)
        self.assertEqual(pos.y_mm, 20.0)

    def test_immutability(self):
        pos = Position2D(10.0, 20.0)
        with self.assertRaises(AttributeError):
            pos.x_mm = 99.0

    def test_distance(self):
        a = Position2D(0.0, 0.0)
        b = Position2D(3.0, 4.0)
        self.assertAlmostEqual(a.distance_to(b), 5.0)

    def test_type_validation(self):
        with self.assertRaises(TypeError):
            Position2D("bad", 0.0)


class TestSpeed(unittest.TestCase):
    """Test Speed value object."""

    def test_negative_rejected(self):
        with self.assertRaises(ValueError):
            Speed(-1.0)

    def test_zero_is_zero(self):
        self.assertTrue(Speed(0.0).is_zero)


class TestScanRecipe(unittest.TestCase):
    """Test ScanRecipe validation."""

    def test_valid_creation(self):
        recipe = ScanRecipe("raster", 12.0, 800.0, 2.5, 50.0, 50.0, 50.0)
        self.assertEqual(recipe.pattern, "raster")
        self.assertAlmostEqual(recipe.line_spacing_mm, 1.25)

    def test_invalid_pattern(self):
        with self.assertRaises(ValueError):
            ScanRecipe("zigzag", 12.0, 800.0, 2.5, 50.0, 50.0, 50.0)

    def test_negative_speed(self):
        with self.assertRaises(ValueError):
            ScanRecipe("raster", -1.0, 800.0, 2.5, 50.0, 50.0, 50.0)


class TestEquipmentSpec(unittest.TestCase):
    """Test EquipmentSpec value object."""

    def test_total_cost(self):
        spec = EquipmentSpec("laser", "Test", "Desc", 1000.0, 3)
        self.assertEqual(spec.total_cost_eur, 3000.0)

    def test_negative_cost_rejected(self):
        with self.assertRaises(ValueError):
            EquipmentSpec("laser", "Bad", "Desc", -100.0)


# ══════════════════════════════════════════════════════════════════════
#                       ENTITY TESTS
# ══════════════════════════════════════════════════════════════════════

class TestXYTable(unittest.TestCase):
    """Test XYTable entity state machine and validation."""

    def test_home(self):
        table = XYTable()
        table.home()
        self.assertTrue(table.is_homed)
        self.assertEqual(table.position, Position2D(0.0, 0.0))

    def test_move_requires_homing(self):
        table = XYTable()
        with self.assertRaises(TableNotHomedError):
            table.move_to(Position2D(10.0, 10.0))

    def test_travel_limit_enforced(self):
        table = XYTable(travel_x_mm=100.0)
        table.home()
        with self.assertRaises(TableLimitError):
            table.move_to(Position2D(150.0, 0.0))

    def test_valid_move(self):
        table = XYTable()
        table.home()
        table.move_to(Position2D(50.0, 75.0))
        self.assertEqual(table.position.x_mm, 50.0)
        self.assertEqual(table.position.y_mm, 75.0)


class TestLaserUnit(unittest.TestCase):
    """Test LaserUnit entity and interlock enforcement."""

    def test_set_power(self):
        laser = LaserUnit(max_power_w=1000)
        laser.set_power(500)
        self.assertEqual(laser.current_power_w, 500)

    def test_power_limit_enforced(self):
        laser = LaserUnit(max_power_w=1000)
        with self.assertRaises(LaserPowerError):
            laser.set_power(1500)

    def test_arm_requires_interlocks(self):
        laser = LaserUnit()
        safety = SafetySystem()  # All unlocked by default
        with self.assertRaises(LaserInterlockError):
            laser.arm(safety)

    def test_arm_with_interlocks(self):
        laser = LaserUnit()
        safety = SafetySystem()
        safety.lock_door()
        safety.lock_chamber()
        laser.arm(safety)
        self.assertEqual(laser.state, LaserState.ARMED)

    def test_fire_requires_arm(self):
        laser = LaserUnit()
        safety = SafetySystem()
        safety.lock_door()
        safety.lock_chamber()
        with self.assertRaises(LaserInterlockError):
            laser.fire(safety)


class TestGasSystem(unittest.TestCase):
    """Test GasSystem entity."""

    def test_set_flow(self):
        gas = GasSystem()
        gas.set_flow(15.0)
        self.assertEqual(gas.current_flow.value_l_per_min, 15.0)
        self.assertEqual(gas.state, GasState.FLOWING)

    def test_flow_limit(self):
        gas = GasSystem(max_flow_l_min=20.0)
        with self.assertRaises(GasFlowError):
            gas.set_flow(25.0)


class TestSafetySystem(unittest.TestCase):
    """Test SafetySystem entity."""

    def test_all_interlocks_require_both(self):
        safety = SafetySystem()
        self.assertFalse(safety.all_interlocks_locked)
        safety.lock_door()
        self.assertFalse(safety.all_interlocks_locked)
        safety.lock_chamber()
        self.assertTrue(safety.all_interlocks_locked)

    def test_estop_blocks_interlocks(self):
        safety = SafetySystem()
        safety.lock_door()
        safety.lock_chamber()
        safety.press_e_stop()
        self.assertFalse(safety.all_interlocks_locked)

    def test_estop_is_latching(self):
        safety = SafetySystem()
        safety.press_e_stop()
        self.assertTrue(safety.e_stop_pressed)
        safety.reset_e_stop()
        self.assertFalse(safety.e_stop_pressed)


# ══════════════════════════════════════════════════════════════════════
#                   INFRASTRUCTURE TESTS
# ══════════════════════════════════════════════════════════════════════

class TestVirtualTableController(unittest.TestCase):
    """Test G-code generation and scan path generation."""

    def setUp(self):
        self.ctrl = VirtualTableController()
        self.table = XYTable()
        self.table.home()

    def test_raster_scan(self):
        recipe = ScanRecipe("raster", 12.0, 800.0, 2.5, 50.0, 30.0, 30.0)
        path = self.ctrl.execute_scan(self.table, recipe)
        self.assertGreater(len(path), 0)

    def test_spiral_scan(self):
        recipe = ScanRecipe("spiral", 12.0, 800.0, 2.5, 50.0, 30.0, 30.0)
        path = self.ctrl.execute_scan(self.table, recipe)
        self.assertGreater(len(path), 0)

    def test_linear_scan(self):
        recipe = ScanRecipe("linear", 12.0, 800.0, 2.5, 50.0, 30.0, 30.0)
        path = self.ctrl.execute_scan(self.table, recipe)
        self.assertGreater(len(path), 0)

    def test_gcode_generation(self):
        recipe = ScanRecipe("raster", 12.0, 800.0, 2.5, 50.0, 20.0, 20.0)
        gcode = self.ctrl.generate_gcode(recipe)
        self.assertGreater(len(gcode), 5)
        self.assertEqual(gcode[0].code, "G90")   # Absolute mode
        self.assertEqual(gcode[1].code, "G21")   # Metric
        self.assertEqual(gcode[-1].code, "M2")   # End program


class TestEquipmentCatalog(unittest.TestCase):
    """Test cost calculator."""

    def test_catalog_not_empty(self):
        catalog = EquipmentCostCatalog()
        items = catalog.get_catalog()
        self.assertGreater(len(items), 20)

    def test_total_calculation(self):
        catalog = EquipmentCostCatalog()
        total = catalog.calculate_total(catalog.get_catalog())
        self.assertGreater(total, 50000)

    def test_category_filter(self):
        catalog = EquipmentCostCatalog()
        lasers = catalog.get_by_category("laser")
        self.assertGreater(len(lasers), 0)
        for item in lasers:
            self.assertEqual(item.category, "laser")


class TestAuditLogger(unittest.TestCase):
    """Test audit logger (STRIDE: Repudiation defense)."""

    def test_log_action(self):
        logger = InMemoryAuditLogger()
        event_id = logger.log_action("TABLE", "HOME", {"x": 0})
        self.assertIsInstance(event_id, str)
        self.assertGreater(len(event_id), 0)

    def test_recent_logs(self):
        logger = InMemoryAuditLogger()
        logger.log_action("TABLE", "HOME", {})
        logger.log_action("LASER", "FIRE", {"power": 800})
        logs = logger.get_recent_logs(10)
        self.assertEqual(len(logs), 2)
        self.assertEqual(logs[0]["subsystem"], "TABLE")

    def test_safety_event(self):
        logger = InMemoryAuditLogger()
        event_id = logger.log_safety_event("E_STOP", {})
        logs = logger.get_recent_logs(1)
        self.assertEqual(logs[0]["severity"], "WARNING")


# ══════════════════════════════════════════════════════════════════════
#                   INTEGRATION TESTS
# ══════════════════════════════════════════════════════════════════════

class TestLabControlIntegration(unittest.TestCase):
    """Integration test: full lab workflow through use case."""

    def test_full_workflow(self):
        lab = create_lab_control()

        # 1. Home table
        result = lab.home_table()
        self.assertEqual(result["status"], "homed")

        # 2. Lock interlocks
        lab.set_door_interlock(True)
        lab.set_chamber_interlock(True)

        # 3. Set laser power and arm
        lab.set_laser_power(800)
        lab.arm_laser()
        self.assertEqual(lab.laser.state, LaserState.ARMED)

        # 4. Fire laser
        lab.fire_laser()
        self.assertEqual(lab.laser.state, LaserState.FIRING)

        # 5. Run a scan
        recipe = ScanRecipe("raster", 12.0, 800.0, 2.5, 50.0, 30.0, 30.0)
        result = lab.run_scan(recipe)
        self.assertGreater(result["total_points"], 0)

        # 6. Stop laser
        lab.stop_laser()
        self.assertEqual(lab.laser.state, LaserState.STANDBY)

        # 7. Check audit trail exists
        logs = lab.get_audit_logs(100)
        self.assertGreater(len(logs), 5)

    def test_safety_prevents_firing(self):
        lab = create_lab_control()
        lab.set_laser_power(800)
        # Don't lock interlocks — arm should fail
        with self.assertRaises(LaserInterlockError):
            lab.arm_laser()

    def test_estop_stops_everything(self):
        lab = create_lab_control()
        lab.home_table()
        lab.set_door_interlock(True)
        lab.set_chamber_interlock(True)
        lab.set_laser_power(800)
        lab.arm_laser()
        lab.fire_laser()

        # Hit E-stop
        lab.trigger_e_stop()
        self.assertEqual(lab.laser.state, LaserState.OFF)
        self.assertEqual(lab.table.state, TableState.ERROR)


if __name__ == "__main__":
    unittest.main()
