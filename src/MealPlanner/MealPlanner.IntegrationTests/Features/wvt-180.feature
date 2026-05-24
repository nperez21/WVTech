Feature: Automatically add shopping list to pantry after purchase
# WVT-180

  Background:
    Given there is a user named 'Gary'
    And 'Gary' is logged into Onebite

  Scenario: Add to pantry modal appears after successful Kroger export
    Given 'Gary' has the following items pending in the pantry modal:
      | Name | Amount | Measurement |
      | Eggs | 12     | Count       |
    When 'Gary' is on the shopping list page
    Then a modal is visible asking whether to add items to the pantry

  Scenario: User confirms adding exported items to their pantry
    Given 'Gary' has the following items pending in the pantry modal:
      | Name   | Amount | Measurement |
      | Egg    | 12     | Count       |
      | Milk   | 2      | Cup         |
    When 'Gary' is on the shopping list page
    And 'Gary' clicks confirm on the add-to-pantry modal
    Then 'Gary' is redirected to the pantry page
    And the pantry page displays 'Egg'
    And the pantry page displays 'Milk'

  Scenario: User cancels adding exported items to their pantry
    Given 'Gary' has the following items pending in the pantry modal:
      | Name   | Amount | Measurement |
      | Bread  | 1      | Count       |
    When 'Gary' is on the shopping list page
    And 'Gary' clicks cancel on the add-to-pantry modal
    Then 'Gary' remains on the shopping list page
    And the pantry page does not display 'Bread'
